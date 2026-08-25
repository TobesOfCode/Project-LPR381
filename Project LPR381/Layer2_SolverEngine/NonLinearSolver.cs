using System;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LPR381.Layer2_SolverEngine
{
    // -------------------------------------------------------------------------
    // CLASS: NonLinearSolver (Layer 2 Solver Engine - 10 Marks Bonus)
    // Purpose: Solves ANY non-linear single-variable optimization problem (e.g. f(x) = x^2,
    // f(x) = x^3 - 3*x + 2, f(x) = sin(x) + x^2) using numerical finite-difference gradients.
    // -------------------------------------------------------------------------
    public class NonLinearSolver
    {
        public StringBuilder IterationLog { get; private set; } = new StringBuilder();

        // -------------------------------------------------------------------------
        // METHOD: SolveExpression
        // Purpose: Main entry point for arbitrary non-linear optimization.
        // How it works: Evaluates any mathematical string formula dynamically at x_k,
        // estimates the derivative numerically via central finite differences,
        // and updates x_k along the negative (or positive) gradient direction.
        // -------------------------------------------------------------------------
        public void SolveExpression(
            string functionExpression,
            bool isMaximize,
            double startingX = 4.0,
            double learningRate = 0.1,
            int maxIterations = 50,
            double minBound = double.NegativeInfinity,
            double maxBound = double.PositiveInfinity)
        {
            IterationLog.Clear();
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==============================================================");
            Console.WriteLine("          GENERAL NON-LINEAR OPTIMIZATION SOLVER              ");
            Console.WriteLine("    Algorithm: Finite-Difference Projected Gradient Descent   ");
            Console.WriteLine("==============================================================");
            Console.ResetColor();

            string optGoal = isMaximize ? "Maximize" : "Minimize";
            string constraintText = (double.IsInfinity(minBound) && double.IsInfinity(maxBound))
                ? "Unconstrained (All Real Numbers)"
                : $"{minBound:F2} <= x <= {maxBound:F2}";

            Console.WriteLine($"\n  Target Function : f(x) = {functionExpression}");
            Console.WriteLine($"  Goal            : {optGoal}");
            Console.WriteLine($"  Constraints     : {constraintText}");
            Console.WriteLine($"  Initial Guess   : x_0 = {startingX:F3}");
            Console.WriteLine($"  Step Size       : alpha = {learningRate:F3}\n");

            // Build uniform ASCII table
            Console.WriteLine("+------+------------+------------+------------+");
            Console.WriteLine("| Iter |          x |       f(x) |      f'(x) |");
            Console.WriteLine("+------+------------+------------+------------+");

            IterationLog.AppendLine("+------+------------+------------+------------+");
            IterationLog.AppendLine("| Iter |          x |       f(x) |      f'(x) |");
            IterationLog.AppendLine("+------+------------+------------+------------+");

            double currentX = startingX;
            double h = 1e-5; // Finite difference step for numerical derivative

            for (int iter = 1; iter <= maxIterations; iter++)
            {
                double fx = Evaluate(functionExpression, currentX);

                // Central difference approximation: f'(x) ~ (f(x+h) - f(x-h)) / (2*h)
                double fxPlus = Evaluate(functionExpression, currentX + h);
                double fxMinus = Evaluate(functionExpression, currentX - h);
                double gradient = (fxPlus - fxMinus) / (2.0 * h);

                // Clean floating point rounding noise
                if (Math.Abs(gradient) < 1e-7) gradient = 0.0;
                if (Math.Abs(fx) < 1e-7) fx = 0.0;

                string rowStr = $"| {iter,4} | {currentX,10:F4} | {fx,10:F4} | {gradient,10:F4} |";
                Console.WriteLine(rowStr);
                IterationLog.AppendLine(rowStr);

                // Convergence test: slope is practically zero
                if (Math.Abs(gradient) < 1e-4)
                {
                    break;
                }

                // Gradient step: x_{k+1} = x_k - alpha * f'(x) (for Min), + alpha * f'(x) (for Max)
                if (isMaximize)
                {
                    currentX += learningRate * gradient;
                }
                else
                {
                    currentX -= learningRate * gradient;
                }

                // Apply Optional Constraint Projection (Clamping to boundary if provided)
                if (currentX < minBound) currentX = minBound;
                if (currentX > maxBound) currentX = maxBound;
            }

            Console.WriteLine("+------+------------+------------+------------+");
            IterationLog.AppendLine("+------+------------+------------+------------+");

            double optimalFx = Evaluate(functionExpression, currentX);
            if (Math.Abs(optimalFx) < 1e-7) optimalFx = 0.0;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n==============================================================");
            Console.WriteLine("              OPTIMAL NON-LINEAR SOLUTION                     ");
            Console.WriteLine("==============================================================");
            Console.WriteLine($"  Optimal Point      : x*    = {currentX:F4}");
            Console.WriteLine($"  Optimal Objective  : f(x*) = {optimalFx:F4}");
            Console.WriteLine("==============================================================");
            Console.ResetColor();

            Console.WriteLine("\n  Press any key to return to menu...");
            Console.ReadKey();
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: Evaluate
        // Purpose: Evaluates general mathematical string expressions for any x value.
        // Supports: basic arithmetic (+, -, *, /), powers (^), sqrt, sin, cos, exp.
        // -------------------------------------------------------------------------
        public double Evaluate(string expression, double x)
        {
            string formatted = expression.ToLower().Replace(" ", "");

            // Handle implicit multiplications like 2x or 3(x) -> 2*x or 3*(x)
            formatted = Regex.Replace(formatted, @"(\d)x", "$1*x");
            formatted = Regex.Replace(formatted, @"(\d)\(", "$1*(");
            formatted = Regex.Replace(formatted, @"\)(\d)", ")*$1");
            formatted = Regex.Replace(formatted, @"\)x", ")*x");

            // Substitute x with high-precision invariant string
            string xStr = "(" + x.ToString("G", CultureInfo.InvariantCulture) + ")";
            formatted = Regex.Replace(formatted, @"\bx\b", xStr);

            // Handle common non-linear functions
            formatted = ProcessMathFunctions(formatted);

            // Evaluate standard polynomial powers like u^2 or u^3
            formatted = ProcessPowers(formatted);

            // Use DataTable compute engine for final arithmetic
            DataTable table = new DataTable();
            var result = table.Compute(formatted, string.Empty);
            return Convert.ToDouble(result, CultureInfo.InvariantCulture);
        }

        private string ProcessMathFunctions(string expr)
        {
            expr = Regex.Replace(expr, @"sin\(([^)]+)\)", m => Math.Sin(Convert.ToDouble(new DataTable().Compute(m.Groups[1].Value, null), CultureInfo.InvariantCulture)).ToString("G", CultureInfo.InvariantCulture));
            expr = Regex.Replace(expr, @"cos\(([^)]+)\)", m => Math.Cos(Convert.ToDouble(new DataTable().Compute(m.Groups[1].Value, null), CultureInfo.InvariantCulture)).ToString("G", CultureInfo.InvariantCulture));
            expr = Regex.Replace(expr, @"sqrt\(([^)]+)\)", m => Math.Sqrt(Convert.ToDouble(new DataTable().Compute(m.Groups[1].Value, null), CultureInfo.InvariantCulture)).ToString("G", CultureInfo.InvariantCulture));
            expr = Regex.Replace(expr, @"exp\(([^)]+)\)", m => Math.Exp(Convert.ToDouble(new DataTable().Compute(m.Groups[1].Value, null), CultureInfo.InvariantCulture)).ToString("G", CultureInfo.InvariantCulture));
            return expr;
        }

        private string ProcessPowers(string expr)
        {
            while (expr.Contains("^"))
            {
                Match match = Regex.Match(expr, @"(\(?-?[\d.]+\)?)\^(\(?-?[\d.]+\)?)");
                if (!match.Success) break;

                string baseValStr = match.Groups[1].Value.Replace("(", "").Replace(")", "");
                string expValStr = match.Groups[2].Value.Replace("(", "").Replace(")", "");

                double b = double.Parse(baseValStr, CultureInfo.InvariantCulture);
                double p = double.Parse(expValStr, CultureInfo.InvariantCulture);

                double powResult = Math.Pow(b, p);
                expr = expr.Substring(0, match.Index) + powResult.ToString("G", CultureInfo.InvariantCulture) + expr.Substring(match.Index + match.Length);
            }
            return expr;
        }
    }
}