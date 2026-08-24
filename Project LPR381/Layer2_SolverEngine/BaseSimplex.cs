using System;
using System.Collections.Generic;
using System.Text;
using LPR381.Models;

namespace LPR381.Layer2_SolverEngine
{
    // Layer 2: The Base Solver Engine.
    // Think of this class as the main calculator of our program. 
    // It takes the rules from the text file, turns them into a giant math grid (a tableau), 
    // and calculates the best possible answer step-by-step using the Primal Simplex algorithm.
    public class BaseSimplex
    {
        // -------------------------------------------------------------------------
        // PROPERTIES (The memory storage for our calculator)
        // -------------------------------------------------------------------------

        // This is our main 2D grid. Imagine it as a giant Excel spreadsheet where all the math happens.
        public double[,] Tableau { get; private set; }

        // These keep track of how tall (rows) and wide (columns) our grid is.
        public int NumRows { get; private set; }
        public int NumCols { get; private set; }

        // These hold the labels for our grid so we know what variables we are looking at (like "x1", "s1", "Z").
        public string[] ColumnHeaders { get; private set; }
        public string[] RowHeaders { get; private set; }

        // We use this to save a text copy of every single grid we create. 
        // Later, Layer 3 will write this massive string to the final text file.
        public StringBuilder IterationLog { get; private set; } = new StringBuilder();

        // Keeps track of whether we are trying to maximize profit or minimize cost.
        private bool isMaximization;

        // A safe copy of the original problem so we can double-check our answers at the very end.
        private LinearModel originalModel;

        // -------------------------------------------------------------------------
        // METHOD: InitializeTableau
        // Purpose: Builds the very first starting grid using pure Slack and Excess logic.
        // How it works: It splits "=" rules into "<=" and ">=", then assigns Slack 
        // variables to "<=" and Excess variables to ">=". No Big-M penalties allowed!
        // -------------------------------------------------------------------------
        public void InitializeTableau(LinearModel model)
        {
            originalModel = model;
            isMaximization = model.OptimizationType == "max";

            // Step 1: Pre-process constraints. We do not use Big-M. 
            // Instead, if we see an exact equal (=) rule, we split it into two separate rules!
            List<Constraint> processedConstraints = new List<Constraint>();
            foreach (var c in model.Constraints)
            {
                if (c.Relation == "=")
                {
                    // Split into a Less-Than-Or-Equal AND a Greater-Than-Or-Equal
                    processedConstraints.Add(new Constraint { Coefficients = new List<double>(c.Coefficients), Relation = "<=", RHS = c.RHS });
                    processedConstraints.Add(new Constraint { Coefficients = new List<double>(c.Coefficients), Relation = ">=", RHS = c.RHS });
                }
                else
                {
                    processedConstraints.Add(c);
                }
            }

            int numDecisionVars = model.ObjectiveCoefficients.Count;
            int numSlacks = 0;
            int numExcess = 0;

            // Step 2: Count what kind of extra columns we need based on the split rules.
            foreach (var c in processedConstraints)
            {
                if (c.Relation == "<=") numSlacks++;
                else if (c.Relation == ">=") numExcess++;
            }

            // Step 3: Set the exact size of our 2D grid.
            NumRows = processedConstraints.Count + 1;
            NumCols = numDecisionVars + numSlacks + numExcess + 1;

            Tableau = new double[NumRows, NumCols];
            ColumnHeaders = new string[NumCols];
            RowHeaders = new string[NumRows];

            // Step 4: Create names for all our columns (x1, s1, e1...) and line them up.
            for (int j = 0; j < numDecisionVars; j++) ColumnHeaders[j] = "x" + (j + 1);

            int currentSlack = numDecisionVars;
            int currentExcess = numDecisionVars + numSlacks;

            int sIdx = 1, eIdx = 1;
            for (int j = 0; j < numSlacks; j++) ColumnHeaders[currentSlack + j] = "s" + (sIdx++);
            for (int j = 0; j < numExcess; j++) ColumnHeaders[currentExcess + j] = "e" + (eIdx++);

            ColumnHeaders[NumCols - 1] = "RHS";
            RowHeaders[0] = "Z";

            // Step 5: Put the objective function values in the top row (the Z-Row).
            for (int j = 0; j < numDecisionVars; j++)
            {
                Tableau[0, j] = isMaximization ? -model.ObjectiveCoefficients[j] : model.ObjectiveCoefficients[j];
            }

            // Step 6: Load the constraints into the grid and give them their specific extra variables.
            for (int i = 0; i < processedConstraints.Count; i++)
            {
                int rowIndex = i + 1;
                var con = processedConstraints[i];

                for (int j = 0; j < numDecisionVars; j++) Tableau[rowIndex, j] = con.Coefficients[j];
                Tableau[rowIndex, NumCols - 1] = con.RHS;

                if (con.Relation == "<=")
                {
                    Tableau[rowIndex, currentSlack] = 1.0; // Slack variable gets a +1
                    RowHeaders[rowIndex] = ColumnHeaders[currentSlack];
                    currentSlack++;
                }
                else if (con.Relation == ">=")
                {
                    Tableau[rowIndex, currentExcess] = -1.0; // Excess variable gets a -1
                    RowHeaders[rowIndex] = ColumnHeaders[currentExcess];

                    // To make sure this row works cleanly in our starting grid, we mathematically 
                    // invert the entire row (multiply by -1). This makes the basic variable positive (+1), 
                    // but flips the RHS to negative.
                    for (int j = 0; j < NumCols; j++)
                    {
                        Tableau[rowIndex, j] *= -1.0;
                    }
                    currentExcess++;
                }
            }
        }

        // -------------------------------------------------------------------------
        // METHOD: Solve
        // Purpose: The main engine loop. It keeps making adjustments (pivots) until 
        // we find the perfect, most optimal answer.
        // -------------------------------------------------------------------------
        public void Solve()
        {
            int iterationCount = 0;
            LogAndPrintTableau(iterationCount);

            while (true)
            {
                // Safety net: If the math gets stuck in an endless loop, stop the computer so it doesn't freeze.
                if (iterationCount > 1000) throw new Exception("Algorithm failed to converge (Infinite loop detected).");

                // Look for the best variable to bring into our solution.
                int pivotCol = GetEnteringVariable();

                // If we can't find any negative numbers left in the Z-Row, we are done! The current grid is optimal.
                if (pivotCol == -1)
                {
                    string optMsg = "\n[SYSTEM] Optimality criterion satisfied. No negative coefficients in Z-Row.";
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(optMsg);
                    Console.ResetColor();
                    IterationLog.AppendLine(optMsg);
                    break;
                }

                // Figure out which rule limits us the most, so we know which variable has to leave our solution.
                int pivotRow = GetLeavingVariable(pivotCol);
                if (pivotRow == -1)
                {
                    // If there are no limits, it means our profit can grow forever. This is mathematically broken.
                    throw new InvalidOperationException("The model is UNBOUNDED. The pivot column contains no positive limit ratios.");
                }

                string pivotMsg = $"\n[PIVOT STEP] Entering: {ColumnHeaders[pivotCol]} | Leaving: {RowHeaders[pivotRow]}";
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(pivotMsg);
                Console.ResetColor();
                IterationLog.AppendLine(pivotMsg);

                // Do the actual math to update the grid for the next round.
                PerformPivot(pivotRow, pivotCol);
                iterationCount++;
                LogAndPrintTableau(iterationCount);
            }

            // Before we finish, do a final check to ensure the math didn't lie to us.
            CheckFeasibility();
        }

        // -------------------------------------------------------------------------
        // METHOD: GetEnteringVariable
        // Purpose: Finds the most negative number in the Z-row.
        // How it works: It scans the top row. The most negative number tells us 
        // which variable will give us the biggest jump in profit.
        // -------------------------------------------------------------------------
        private int GetEnteringVariable()
        {
            int enteringCol = -1;
            double minValue = -1e-7; // A tiny tolerance so the computer doesn't get confused by rounding errors

            for (int j = 0; j < NumCols - 1; j++)
            {
                if (Tableau[0, j] < minValue)
                {
                    minValue = Tableau[0, j];
                    enteringCol = j;
                }
            }
            return enteringCol;
        }

        // -------------------------------------------------------------------------
        // METHOD: GetLeavingVariable
        // Purpose: Performs the "Minimum Ratio Test".
        // How it works: It takes the RHS (capacity) and divides it by the pivot column.
        // The smallest positive ratio tells us which constraint will run out of space first.
        // -------------------------------------------------------------------------
        private int GetLeavingVariable(int pivotCol)
        {
            int leavingRow = -1;
            double minRatio = double.MaxValue;

            for (int i = 1; i < NumRows; i++)
            {
                double coefficient = Tableau[i, pivotCol];
                if (coefficient > 1e-7) // We ONLY divide by positive numbers to find the limit!
                {
                    double rhs = Tableau[i, NumCols - 1];
                    double ratio = rhs / coefficient;

                    // Primal Simplex strictly requires non-negative ratios.
                    if (ratio >= 0 && ratio < minRatio)
                    {
                        minRatio = ratio;
                        leavingRow = i;
                    }
                }
            }
            return leavingRow;
        }

        // -------------------------------------------------------------------------
        // METHOD: PerformPivot
        // Purpose: The core math that manipulates the 2D array grid.
        // How it works: It divides the pivot row to make the pivot number exactly 1.
        // Then, it subtracts multiples of that row from all other rows to turn the rest of the column into 0s.
        // -------------------------------------------------------------------------
        private void PerformPivot(int pivotRow, int pivotCol)
        {
            // Swap the label on the left side to show the new variable has entered.
            RowHeaders[pivotRow] = ColumnHeaders[pivotCol];
            double pivotValue = Tableau[pivotRow, pivotCol];

            // Normalize the row (make the pivot point 1)
            for (int j = 0; j < NumCols; j++) Tableau[pivotRow, j] /= pivotValue;

            // Zero out every other row in that column
            for (int i = 0; i < NumRows; i++)
            {
                if (i != pivotRow)
                {
                    double factor = Tableau[i, pivotCol];
                    for (int j = 0; j < NumCols; j++)
                    {
                        Tableau[i, j] -= factor * Tableau[pivotRow, j];
                    }
                }
            }
        }

        // -------------------------------------------------------------------------
        // METHOD: CheckFeasibility
        // Purpose: Double-checks the final answer against reality.
        // How it works: It checks if the final math violates the original text file's rules.
        // -------------------------------------------------------------------------
        private void CheckFeasibility()
        {
            SimplexResult currentResult = GetResult();

            // Verify the answers against the literal text boundaries from the original file.
            for (int i = 0; i < originalModel.Constraints.Count; i++)
            {
                double lhsSum = 0;
                for (int j = 0; j < originalModel.Constraints[i].Coefficients.Count; j++)
                {
                    string varName = "x" + (j + 1);
                    lhsSum += originalModel.Constraints[i].Coefficients[j] * currentResult.VariableValues[varName];
                }

                double rhs = originalModel.Constraints[i].RHS;
                string relation = originalModel.Constraints[i].Relation;

                bool isFeasible = true;
                if (relation == "<=" && lhsSum > rhs + 1e-5) isFeasible = false;
                else if (relation == ">=" && lhsSum < rhs - 1e-5) isFeasible = false;
                else if (relation == "=" && Math.Abs(lhsSum - rhs) > 1e-5) isFeasible = false;

                if (!isFeasible)
                {
                    string lhsStr = lhsSum.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                    string rhsStr = rhs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                    throw new InvalidOperationException(
                        $"Mathematical Feasibility Failed on Constraint {i + 1}!\n" +
                        $"  >> Equation evaluated: {lhsStr} {relation} {rhsStr}\n" +
                        $"  >> Reason: The constraints conflict and form an empty feasible region."
                    );
                }
            }
        }

        // -------------------------------------------------------------------------
        // METHOD: GetResult
        // Purpose: Packages the final optimal numbers into our DTO object.
        // How it works: It creates a clean clone of the grid and builds a dictionary 
        // of variables so other team members can easily use our results.
        // -------------------------------------------------------------------------
        public SimplexResult GetResult()
        {
            SimplexResult result = new SimplexResult();
            result.OptimalZ = Tableau[0, NumCols - 1];

            // Deep clone the array so nobody accidentally breaks our actual grid later.
            result.FinalTableau = (double[,])Tableau.Clone();
            result.RowHeaders = (string[])RowHeaders.Clone();
            result.ColumnHeaders = (string[])ColumnHeaders.Clone();

            // Match up the variable names with their final numbers.
            for (int j = 0; j < NumCols - 1; j++)
            {
                string varName = ColumnHeaders[j];
                int rowIndex = Array.IndexOf(RowHeaders, varName);

                if (rowIndex != -1) result.VariableValues[varName] = Tableau[rowIndex, NumCols - 1];
                else result.VariableValues[varName] = 0.0;
            }

            return result;
        }

        // -------------------------------------------------------------------------
        // METHOD: LogAndPrintTableau
        // Purpose: Draws the beautiful text grid for our iterations.
        // How it works: It measures the length of every number and name to dynamically 
        // calculate exactly how wide each column needs to be so it aligns perfectly.
        // -------------------------------------------------------------------------
        private void LogAndPrintTableau(int iteration)
        {
            // Measure how wide the labels on the left side need to be.
            int bvColWidth = 6;
            for (int i = 0; i < NumRows; i++)
            {
                if (RowHeaders[i].Length + 2 > bvColWidth) bvColWidth = RowHeaders[i].Length + 2;
            }

            // Measure how wide every other column needs to be to fit its biggest number.
            int[] colWidths = new int[NumCols];
            for (int j = 0; j < NumCols; j++)
            {
                int maxLen = ColumnHeaders[j].Length;
                for (int i = 0; i < NumRows; i++)
                {
                    string numStr = Tableau[i, j].ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                    if (numStr.Length > maxLen) maxLen = numStr.Length;
                }
                colWidths[j] = Math.Max(maxLen + 2, 8);
            }

            StringBuilder sb = new StringBuilder();
            string title = iteration == 0 ? "\n--- Initial Tableau (Canonical Form) ---" : $"\n--- Tableau Iteration {iteration} ---";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(title);
            Console.ResetColor();
            sb.AppendLine(title);

            // Draw the top line (like +----+----+----+ )
            string separator = GenerateAsciiSeparator(bvColWidth, colWidths);
            Console.WriteLine(separator);
            sb.AppendLine(separator);

            // Draw the column names across the top (x1, s1, e1...)
            StringBuilder headerRow = new StringBuilder();
            headerRow.Append(" |").Append(CenterText("B.V", bvColWidth));
            for (int j = 0; j < NumCols; j++) headerRow.Append("|").Append(CenterText(ColumnHeaders[j], colWidths[j]));
            headerRow.Append("|");

            Console.WriteLine(headerRow.ToString());
            sb.AppendLine(headerRow.ToString());

            Console.WriteLine(separator);
            sb.AppendLine(separator);

            // Print the actual numbers row by row
            for (int i = 0; i < NumRows; i++)
            {
                StringBuilder dataRow = new StringBuilder();
                dataRow.Append(" |").Append(CenterText(RowHeaders[i], bvColWidth));
                for (int j = 0; j < NumCols; j++)
                {
                    // Force the numbers to have exactly 3 decimal points, aligned to the right.
                    string val = Tableau[i, j].ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                    dataRow.Append("|").Append(val.PadLeft(colWidths[j] - 1)).Append(" ");
                }
                dataRow.Append("|");

                Console.WriteLine(dataRow.ToString());
                sb.AppendLine(dataRow.ToString());

                // Draw a separator line immediately under the Z-Row to separate the profit from the rules.
                if (i == 0 && NumRows > 1)
                {
                    Console.WriteLine(separator);
                    sb.AppendLine(separator);
                }
            }

            Console.WriteLine(separator);
            sb.AppendLine(separator);

            // Save the beautifully formatted string to our log so the Exporter can write it to the file later!
            IterationLog.Append(sb.ToString());
        }

        // Helper: Generates a flexible ASCII line (e.g. "+--------+--------+") based on our column width measurements.
        private string GenerateAsciiSeparator(int bvWidth, int[] colWidths)
        {
            StringBuilder sep = new StringBuilder();
            sep.Append(" +").Append(new string('-', bvWidth));
            for (int j = 0; j < colWidths.Length; j++) sep.Append("+").Append(new string('-', colWidths[j]));
            sep.Append("+");
            return sep.ToString();
        }

        // Helper: Takes text and wraps it in spaces so it sits perfectly in the center of the column.
        private static string CenterText(string text, int width)
        {
            if (text.Length >= width) return text;
            int leftPadding = (width - text.Length) / 2;
            int rightPadding = width - text.Length - leftPadding;
            return new string(' ', leftPadding) + text + new string(' ', rightPadding);
        }
    }
}