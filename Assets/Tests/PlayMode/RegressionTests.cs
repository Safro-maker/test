using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Reflection allows this test assembly to exercise the project's predefined
// Assembly-CSharp without moving existing scripts or changing prefab references.
public class RegressionTests
{
    private Component grid;
    private readonly List<GameObject> objects = new List<GameObject>();
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type Script(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Call(object obj, string method, params object[] args) =>
        obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
    private static FieldInfo Field(object obj, string name)
    {
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Flags);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }
    private static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    private static T Get<T>(object obj, string name) => (T)Field(obj, name).GetValue(obj);
    private Component Create(string name)
    {
        var go = new GameObject(name);
        objects.Add(go);
        var component = go.AddComponent(Script(name));
        ((Behaviour)component).enabled = false;
        return component;
    }
    private Component UnitAt(Vector2Int cell, string type = "Unit")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        objects.Add(go);
        var unit = go.AddComponent(Script(type));
        ((Behaviour)unit).enabled = false;
        Call(unit, "Init", cell, null);
        return unit;
    }
    private Component[] All(string name)
    {
        var found = Object.FindObjectsByType(Script(name), FindObjectsSortMode.None);
        return Array.ConvertAll(found, item => (Component)item);
    }
    private Component Waves(out Component spawner)
    {
        spawner = Create("SquadSpawner");
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        objects.Add(prefab);
        ((Behaviour)prefab.AddComponent(Script("Unit"))).enabled = false;
        Set(spawner, "enemyUnitPrefab", prefab);
        return Create("WaveManager");
    }
    [SetUp]
    public void SetUp()
    {
        grid = Create("GridManager");
        Set(grid, "size", 12);
    }
    [TearDown]
    public void TearDown()
    {
        // SpawnWave creates objects independently of the fixture's Create helper.
        foreach (var unit in All("Unit")) if (unit != null) Object.DestroyImmediate(unit.gameObject);
        foreach (var squad in All("Squad")) if (squad != null) Object.DestroyImmediate(squad.gameObject);
        foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
        objects.Clear();
    }
    [Test]
    public void RepeatedStartCannotOverlapWavesOrSkipPause()
    {
        var waves = Waves(out _);
        Call(waves, "StartFirstWave");
        int count = ((IList)Call(grid, "GetAllUnits")).Count;
        Call(waves, "StartFirstWave");
        Call(waves, "StartNextWave");
        Assert.AreEqual(1, Get<int>(waves, "currentWave"));
        Assert.AreEqual(count, ((IList)Call(grid, "GetAllUnits")).Count);
        foreach (var unit in (IList)Call(grid, "GetAllUnits")) Call(unit, "Die", new object[] { null });
        Call(waves, "CheckWaveComplete");
        Call(waves, "StartFirstWave");
        Call(waves, "StartNextWave");
        Assert.AreEqual(1, Get<int>(waves, "currentWave"));
    }
    [TestCase(0f)]
    [TestCase(-1f)]
    public void NonPositivePauseStartsNextWave(float delay)
    {
        var waves = Waves(out _);
        Set(waves, "interWaveDelay", delay);
        Call(waves, "StartFirstWave");
        foreach (var unit in (IList)Call(grid, "GetAllUnits")) Call(unit, "Die", new object[] { null });
        Call(waves, "Update");
        Assert.AreEqual(2, Get<int>(waves, "currentWave"));
        Assert.IsTrue(Get<bool>(waves, "isWaveActive"));
    }
    [Test]
    public void FailedSpawnDoesNotAdvanceWaveOrHealPlayer()
    {
        var waves = Waves(out var spawner);
        Set(spawner, "enemyUnitPrefab", null);
        var player = UnitAt(new Vector2Int(5, 5));
        Set(player, "hp", 2);
        LogAssert.Expect(LogType.Error, "[SquadSpawner] enemyUnitPrefab не назначен — волна отменена.");
        Call(waves, "StartFirstWave");
        Call(waves, "Update");
        Assert.AreEqual(0, Get<int>(waves, "currentWave"));
        Assert.IsFalse(Get<bool>(waves, "isWaveActive"));
        Assert.AreEqual(2, Get<int>(player, "hp"));
    }
    [Test]
    public void SlingerRejectsBlockedShotsAndAcceptsClearAlternative()
    {
        var archer = UnitAt(new Vector2Int(5, 5), "Slinger");
        var target = new Vector2Int(8, 5);
        Assert.IsTrue((bool)Call(archer, "CanAttackCellFrom", new Vector2Int(5, 5), target));
        Call(grid, "SetBlocked", new Vector2Int(6, 5), true);
        Assert.IsFalse((bool)Call(archer, "CanAttackCellFrom", new Vector2Int(5, 5), target));
        Assert.IsTrue((bool)Call(archer, "CanAttackCellFrom", new Vector2Int(7, 6), target));
        Call(grid, "SetBlocked", new Vector2Int(6, 5), false);
        UnitAt(new Vector2Int(6, 5));
        Assert.IsFalse((bool)Call(archer, "CanAttackCellFrom", new Vector2Int(5, 5), target));
    }
    [UnityTest]
    public IEnumerator EmptyPathCancelsExistingMovement()
    {
        var unit = UnitAt(new Vector2Int(1, 1));
        Call(unit, "StartMove", new Vector2Int(1, 1), new Vector2Int(8, 1));
        Assert.IsTrue(Get<bool>(unit, "IsMoving"));
        Call(unit, "StartMoveAlongPath", new List<Vector2Int>(), new Vector2Int(1, 1), new Vector2Int(1, 1));
        var position = unit.transform.position;
        yield return null;
        Assert.IsFalse(Get<bool>(unit, "IsMoving"));
        Assert.AreEqual(position, unit.transform.position);
        Assert.IsNull(Get<Coroutine>(unit, "moveCoroutine"));
    }
    [TestCase(0.1f, 5, 0.2f)]
    [TestCase(0f, 1, 0.2f)]
    public void CombatAccountsForElapsedTime(float interval, int frames, float delta)
    {
        var unit = UnitAt(new Vector2Int(1, 1));
        var combat = Create("CombatResolver");
        Set(combat, "tickInterval", interval);
        Set(unit, "cooldownTimer", 2f);
        for (int i = 0; i < frames; i++) Call(combat, "AdvanceCombat", delta, grid);
        Assert.AreEqual(2f - frames * delta, Get<float>(unit, "cooldownTimer"), 0.001f);
    }
    [UnityTest]
    public IEnumerator PlayerStopsAtNewObstacle()
    {
        var start = new Vector2Int(1, 1);
        var next = new Vector2Int(2, 1);
        var unit = UnitAt(start);
        Set(unit, "moveSpeed", 0.1f);
        Call(unit, "StartMove", start, new Vector2Int(4, 1));
        Call(grid, "SetBlocked", next, true);
        yield return null;
        yield return null;
        Assert.IsFalse(Get<bool>(unit, "IsMoving"));
        Assert.AreEqual(Call(grid, "GridToWorld", start), unit.transform.position);
    }
    [UnityTest]
    public IEnumerator EnemyStopsAtNewObstacleAndReleasesClaim()
    {
        var start = new Vector2Int(1, 1);
        var next = new Vector2Int(2, 1);
        var unit = UnitAt(start);
        Set(unit, "moveSpeed", 0.1f);
        var ai = unit.gameObject.AddComponent(Script("EnemyAI"));
        ((Behaviour)ai).enabled = false;
        Set(ai, "path", new List<Vector2Int> { next });
        Call(ai, "StartMovement", next);
        Call(grid, "SetBlocked", next, true);
        yield return null;
        yield return null;
        Assert.IsFalse(Get<bool>(unit, "IsMoving"));
        Assert.IsNull(Get<object>(ai, "myClaimedCell"));
        Assert.AreEqual(Call(grid, "GridToWorld", start), unit.transform.position);
    }
    [Test]
    public void NewCornerObstacleBlocksDiagonalStep()
    {
        Call(grid, "SetBlocked", new Vector2Int(2, 1), true);
        Assert.IsFalse((bool)Call(grid, "CanTraverse", new Vector2Int(1, 1), new Vector2Int(2, 2)));
    }
}
