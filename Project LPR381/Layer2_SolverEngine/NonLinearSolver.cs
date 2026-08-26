using System;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LPR381.Layer2_SolverEngine
{
    // -------------------------------------------------------------------------
    // CLASS: NonLinearSolver 
    // Purpose: Linear programming relies on perfectly straight lines. This class 
    // breaks that rule to solve curves (Non-Linear problems like f(x) = x^2).
    //
    // How it works: It uses an algorithm called "Gradient Descent". Imagine a 
    // blindfolded hiker trying to find the lowest point of a valley. They can't 
    // see the bottom, but they can feel the slope of the ground under their feet. 
    // By always taking a step downwards (opposite to the upward slope), they will 
    // eventually reach the flat bottom of the valley!
    // -------------------------------------------------------------------------
    public class NonLinearSolver
    {
        // Keeps a text record of every step the algorithm takes so we can export it to a report later.
        public StringBuilder IterationLog { get; private set; } = new StringBuilder();

        // -------------------------------------------------------------------------
        // METHOD: SolveExpression
        // Purpose: The main steering wheel of the algorithm. It takes the mathematical 
        // string, finds its slope at a specific point, and takes steps downward 
        // (to minimize) or upward (to maximize).
        // -------------------------------------------------------------------------
        public void SolveExpression(
            string functionExpression,
            bool isMaximize,
            double startingX = 4.0,                    // x_0: Where the "hiker" starts on the curve
            double learningRate = 0.1,                 // alpha: How big of a step the hiker takes
            int maxIterations = 50,                    // The maximum number of steps allowed before giving up
            double minBound = double.NegativeInfinity, // Optional left wall (constraint)
            double maxBound = double.PositiveInfinity) // Optional right wall (constraint)
        {
            // 1. Clean the slate for a new calculation
            IterationLog.Clear();
            Console.Clear();

            // 2. Print the beautiful UI Header
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==============================================================");
            Console.WriteLine("          GENERAL NON-LINEAR OPTIMIZATION SOLVER              ");
            Console.WriteLine("    Algorithm: Finite-Difference Projected Gradient Descent   ");
            Console.WriteLine("==============================================================");
            Console.ResetColor();

            // 3. Determine if we have boundaries (like a fenced-in yard) or if it's unconstrained
            string optGoal = isMaximize ? "Maximize" : "Minimize";
            string constraintText = (double.IsInfinity(minBound) && double.IsInfinity(maxBound))
                ? "Unconstrained (All Real Numbers)"
                : $"{minBound:F2} <= x <= {maxBound:F2}";

            // 4. Display the problem configuration to the user
            Console.WriteLine($"\n  Target Function : f(x) = {functionExpression}");
            Console.WriteLine($"  Goal            : {optGoal}");
            Console.WriteLine($"  Constraints     : {constraintText}");
            Console.WriteLine($"  Initial Guess   : x_0 = {startingX:F3}");
            Console.WriteLine($"  Step Size       : alpha = {learningRate:F3}\n");

            // 5. Build our uniform ASCII table header for both the Console and the internal Log
            Console.WriteLine("+------+------------+------------+------------+");
            Console.WriteLine("| Iter |          x |       f(x) |      f'(x) |");
            Console.WriteLine("+------+------------+------------+------------+");

            IterationLog.AppendLine("+------+------------+------------+------------+");
            IterationLog.AppendLine("| Iter |          x |       f(x) |      f'(x) |");
            IterationLog.AppendLine("+------+------------+------------+------------+");

            // Set our starting position
            double currentX = startingX;

            // 'h' is a microscopic step used to check the slope. 
            // It represents the "feel the ground slightly ahead and slightly behind" logic.
            double h = 1e-5;

            // =====================================================================
            // THE OPTIMIZATION LOOP (The Hiker Walking)
            // =====================================================================
            for (int iter = 1; iter <= maxIterations; iter++)
            {
                // A. Where are we currently? Calculate the height of the curve: y = f(x)
                double fx = Evaluate(functionExpression, currentX);

                // B. What is the slope (derivative) right here? 
                // We use the "Central Finite Difference" formula to estimate the slope 
                // without needing complex calculus. We look slightly forward (fxPlus) 
                // and slightly backward (fxMinus), then divide by the total distance (2 * h).
                double fxPlus = Evaluate(functionExpression, currentX + h);
                double fxMinus = Evaluate(functionExpression, currentX - h);
                double gradient = (fxPlus - fxMinus) / (2.0 * h);

                // C. Clean up microscopic floating-point rounding errors (e.g., turning 0.000000001 into 0.0)
                if (Math.Abs(gradient) < 1e-7) gradient = 0.0;
                if (Math.Abs(fx) < 1e-7) fx = 0.0;

                // D. Log our current position and slope to the table
                string rowStr = $"| {iter,4} | {currentX,10:F4} | {fx,10:F4} | {gradient,10:F4} |";
                Console.WriteLine(rowStr);
                IterationLog.AppendLine(rowStr);

                // E. Convergence Test: If the slope is basically flat (zero), we have 
                // reached the bottom of the valley (or the top of the hill). Stop walking!
                if (Math.Abs(gradient) < 1e-4) break;

                // F. Take a step! 
                // If maximizing, walk UP the slope (add). If minimizing, walk DOWN the slope (subtract).
                if (isMaximize) currentX += learningRate * gradient;
                else currentX -= learningRate * gradient;

                // G. Projected Constraints: If the user provided a boundary, don't let 'x' walk past it! 
                // If the math tells us to step past the wall, we just hug the wall instead.
                if (currentX < minBound) currentX = minBound;
                if (currentX > maxBound) currentX = maxBound;
            }

            // =====================================================================
            // WRAP UP & DISPLAY RESULTS
            // =====================================================================
            Console.WriteLine("+------+------------+------------+------------+");
            IterationLog.AppendLine("+------+------------+------------+------------+");

            // Do one final evaluation at our stopping point to get the exact optimal value
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
        // Purpose: Computers can't natively do math on text strings like "3x^2 + 5". 
        // This method acts as a translator, turning the human string into a format 
        // the C# engine can actually calculate safely.
        // -------------------------------------------------------------------------
        public double Evaluate(string expression, double x)
        {
            // 1. Standardize the string: lowercase everything and remove spaces
            string formatted = expression.ToLower().Replace(" ", "");

            // 2. Fix human shorthand: Humans write "2x" or "3(x)", but computers need "2*x" or "3*(x)". 
            // These Regex expressions safely insert the missing multiplication stars.
            formatted = Regex.Replace(formatted, @"(\d)x", "$1*x");
            formatted = Regex.Replace(formatted, @"(\d)\(", "$1*(");
            formatted = Regex.Replace(formatted, @"\)(\d)", ")*$1");
            formatted = Regex.Replace(formatted, @"\)x", ")*x");

            // 3. Swap out the letter 'x' for our actual current numerical value.
            // We wrap it in brackets to protect negative numbers (e.g., turning "-x" into "-( -5 )" ).
            string xStr = "(" + x.ToString("G", CultureInfo.InvariantCulture) + ")";
            formatted = Regex.Replace(formatted, @"\bx\b", xStr);

            try
            {
                // 4. DataTable (our ultimate calculator) only understands basic +, -, *, /
                // So we must manually calculate any advanced functions (sin, cos) or powers (x^2) first.
                formatted = ProcessMathFunctions(formatted);
                formatted = ProcessPowers(formatted);

                // 5. Finally, hand the cleaned-up arithmetic string to DataTable to get the final number.
                DataTable table = new DataTable();
                var result = table.Compute(formatted, string.Empty);
                return Convert.ToDouble(result, CultureInfo.InvariantCulture);
            }
            catch (SyntaxErrorException)
            {
                // If a user types a broken formula with missing brackets, catch it gracefully 
                // instead of crashing the entire program!
                throw new InvalidOperationException($"Math Syntax Error! Please check your formula '{expression}' for missing parentheses or unsupported symbols.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not evaluate expression '{expression}': {ex.Message}");
            }
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: ProcessMathFunctions
        // Purpose: Hunts for words like "sin(...)" or "exp(...)", calculates their 
        // actual numeric value, and replaces the word with the raw answer in the string.
        // -------------------------------------------------------------------------
        private string ProcessMathFunctions(string expr)
        {
            // This scary-looking pattern is a "Balanced Group" Regex. It acts like a bracket counter. 
            // It ensures we capture everything inside the outer function brackets, even if there 
            // are nested brackets inside (like a Russian nesting doll), so we don't accidentally chop 
            // formulas like 'sin((4))' in half!
            string balancedPattern = @"\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)";

            // For each function, we find it, compute the math inside the brackets using DataTable, 
            // pass that number to standard C# Math functions, and inject the result back into the string.
            expr = Regex.Replace(expr, @"sin" + balancedPattern, m => Math.Sin(Convert.ToDouble(new DataTable().Compute(m.Groups[1].Value, null), CultureInfo.InvariantCulture)).ToString("G", CultureInfo.InvariantCulture));
            expr = Regex.Replace(expr, @"cos" + balancedPattern, m => Math.Cos(Convert.ToDouble(new DataTable().Compute(m.Groups[1].Value, null), CultureInfo.InvariantCulture)).ToString("G", CultureInfo.InvariantCulture));
            expr = Regex.Replace(expr, @"sqrt" + balancedPattern, m => Math.Sqrt(Convert.ToDouble(new DataTable().Compute(m.Groups[1].Value, null), CultureInfo.InvariantCulture)).ToString("G", CultureInfo.InvariantCulture));
            expr = Regex.Replace(expr, @"exp" + balancedPattern, m => Math.Exp(Convert.ToDouble(new DataTable().Compute(m.Groups[1].Value, null), CultureInfo.InvariantCulture)).ToString("G", CultureInfo.InvariantCulture));

            return expr;
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: ProcessPowers
        // Purpose: Computes exponents (e.g., changing "3^2" into "9") because 
        // standard DataTable.Compute does not understand the "^" symbol natively.
        // -------------------------------------------------------------------------
        private string ProcessPowers(string expr)
        {
            // Keep looking for the '^' symbol until they are all gone
            while (expr.Contains("^"))
            {
                // Finds the base number (left) and the exponent number (right) surrounding the '^'.
                // It also supports Scientific Notation (like E-05) just in case 'x' becomes microscopic.
                Match match = Regex.Match(expr, @"(\(?-?[0-9.]+(?:E[-+]?[0-9]+)?\)?)\^(\(?-?[0-9.]+(?:E[-+]?[0-9]+)?\)?)", RegexOptions.IgnoreCase);
                if (!match.Success) break;

                // Strip away any protective brackets to get the raw numbers
                string baseValStr = match.Groups[1].Value.Replace("(", "").Replace(")", "");
                string expValStr = match.Groups[2].Value.Replace("(", "").Replace(")", "");

                // Convert the strings to decimals
                double b = double.Parse(baseValStr, CultureInfo.InvariantCulture);
                double p = double.Parse(expValStr, CultureInfo.InvariantCulture);

                // Do the actual power math (b to the power of p) and replace the equation chunk 
                // in the original string with the calculated raw number.
                double powResult = Math.Pow(b, p);
                expr = expr.Substring(0, match.Index) + powResult.ToString("G", CultureInfo.InvariantCulture) + expr.Substring(match.Index + match.Length);
            }
            return expr;
        }
    }
}