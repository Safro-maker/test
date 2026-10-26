"""Консольный калькулятор без сторонних зависимостей."""

import ast
import operator


OPERATIONS = {
    ast.Add: operator.add,
    ast.Sub: operator.sub,
    ast.Mult: operator.mul,
    ast.Div: operator.truediv,
}


def calculate(expression: str) -> int | float:
    """Вычислить выражение с числами, скобками и операциями +, -, *, /."""
    expression = expression.strip()
    if not expression:
        raise ValueError("Введите выражение.")
    if len(expression) > 200:
        raise ValueError("Выражение слишком длинное (максимум 200 символов).")

    try:
        tree = ast.parse(expression, mode="eval")
    except SyntaxError as error:
        raise ValueError("Некорректное выражение.") from error

    def evaluate(node: ast.AST) -> int | float:
        if isinstance(node, ast.Constant) and type(node.value) in (int, float):
            return node.value
        if isinstance(node, ast.BinOp) and type(node.op) in OPERATIONS:
            return OPERATIONS[type(node.op)](evaluate(node.left), evaluate(node.right))
        if isinstance(node, ast.UnaryOp):
            if isinstance(node.op, ast.UAdd):
                return evaluate(node.operand)
            if isinstance(node.op, ast.USub):
                return -evaluate(node.operand)
        raise ValueError("Допустимы только числа, скобки и операции +, -, *, /.")

    try:
        return evaluate(tree.body)
    except ZeroDivisionError as error:
        raise ValueError("На ноль делить нельзя.") from error
    except OverflowError as error:
        raise ValueError("Слишком большое число.") from error


def main() -> None:
    print("Калькулятор: +, -, *, / и скобки. Дроби записывайте через точку.")
    print("Для выхода введите «выход», exit или quit.")
    while True:
        try:
            expression = input("> ").strip()
        except (EOFError, KeyboardInterrupt):
            print("\nДо свидания!")
            break
        if expression.lower() in {"выход", "exit", "quit"}:
            print("До свидания!")
            break
        if not expression:
            continue
        try:
            print(f"= {calculate(expression)}")
        except ValueError as error:
            print(f"Ошибка: {error}")


if __name__ == "__main__":
    main()
