import subprocess
import sys
import unittest
from pathlib import Path

from calculator import calculate


class CalculatorTests(unittest.TestCase):
    def test_arithmetic(self):
        for expression, expected in [
            ("2 + 3 * 4", 14),
            ("(2 + 3) * 4", 20),
            ("10 - 4", 6),
            ("7 / 2", 3.5),
            ("-2 * (+3)", -6),
            ("1.5 + 2.25", 3.75),
        ]:
            with self.subTest(expression=expression):
                self.assertEqual(calculate(expression), expected)

    def test_division_by_zero(self):
        with self.assertRaisesRegex(ValueError, "На ноль"):
            calculate("10 / (2 - 2)")

    def test_invalid_expressions(self):
        for expression in ["", "2 +", "hello", "True", "2 ** 3", "abs(-2)", "[1]"]:
            with self.subTest(expression=expression):
                with self.assertRaises(ValueError):
                    calculate(expression)

    def test_length_limit(self):
        with self.assertRaisesRegex(ValueError, "слишком длинное"):
            calculate("1+" * 101 + "1")

    def test_interactive_session(self):
        result = subprocess.run(
            [sys.executable, str(Path(__file__).with_name("calculator.py"))],
            input="2 + 3 * 4\n1 / 0\n(8 - 2) / 3\nвыход\n",
            text=True,
            capture_output=True,
            check=True,
        )
        self.assertIn("= 14", result.stdout)
        self.assertIn("Ошибка: На ноль делить нельзя.", result.stdout)
        self.assertIn("= 2.0", result.stdout)
        self.assertIn("До свидания!", result.stdout)


if __name__ == "__main__":
    unittest.main()
