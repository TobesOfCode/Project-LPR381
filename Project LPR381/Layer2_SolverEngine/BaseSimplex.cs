using System;
using System.Text;
using LPR381.Models;

namespace LPR381.Layer2_SolverEngine
{
    // Layer 2: The Solver Engine.
    // This is the brain of the operation. It takes our organized LinearModel, turns it into a math grid, and runs the Simplex algorithm.
    public class BaseSimplex
    {
        // We use a standard 2D array for the Simplex tableaus. Think of it like a giant Excel spreadsheet.
        public double[,] Tableau { get; private set; }
        public int NumRows { get; private set; }
        public int NumCols { get; private set; }
        public string[] ColumnHeaders { get; private set; }
        public string[] RowHeaders { get; private set; }

        // We store every single iteration of the grid into this string builder so Layer 3 can export it later.
        public StringBuilder IterationLog { get; private set; } = new StringBuilder();

        private bool isMaximization;
        private LinearModel originalModel;

        // Step 1: Build the starting grid.
        public void InitializeTableau(LinearModel model)
        {
            originalModel = model;
            isMaximization = model.OptimizationType == "max";

            // Figure out how big the grid needs to be.
            NumRows = model.Constraints.Count + 1;
            int numDecisionVars = model.ObjectiveCoefficients.Count;
            int numSlackVars = model.Constraints.Count;
            NumCols = numDecisionVars + numSlackVars + 1;

            Tableau = new double[NumRows, NumCols];
            ColumnHeaders = new string[NumCols];
            RowHeaders = new string[NumRows];

            // Setup the labels across the top (x1, x2, s1, s2, RHS)
            for (int j = 0; j < numDecisionVars; j++) ColumnHeaders[j] = "x" + (j + 1);
            for (int j = 0; j < numSlackVars; j++) ColumnHeaders[numDecisionVars + j] = "s" + (j + 1);
            ColumnHeaders[NumCols - 1] = "RHS";

            // Setup the labels down the side (Z, s1, s2)
            RowHeaders[0] = "Z";
            for (int i = 1; i < NumRows; i++) RowHeaders[i] = "s" + i;

            // Load the objective function into the very top row (Row 0).
            for (int j = 0; j < numDecisionVars; j++)
            {
                Tableau[0, j] = isMaximization ? -model.ObjectiveCoefficients[j] : model.ObjectiveCoefficients[j];
            }
            Tableau[0, NumCols - 1] = 0;

            // Load the constraints into the rows underneath.
            for (int i = 0; i < model.Constraints.Count; i++)
            {
                int rowIndex = i + 1;
                for (int j = 0; j < numDecisionVars; j++)
                {
                    Tableau[rowIndex, j] = model.Constraints[i].Coefficients[j];
                }
                Tableau[rowIndex, numDecisionVars + i] = 1.0; // Adding a slack variable!
                Tableau[rowIndex, NumCols - 1] = model.Constraints[i].RHS;
            }
        }

        // Step 2: The main math loop. We keep pivoting the matrix until we find the best answer.
        public void Solve()
        {
            int iterationCount = 0;
            LogAndPrintTableau(iterationCount);

            while (true)
            {
                // Check if we are done: are there any negative numbers left in the Z-Row?
                int pivotCol = GetEnteringVariable();
                if (pivotCol == -1)
                {
                    string optMsg = "\n[SYSTEM] Optimality criterion satisfied. No negative coefficients in Z-Row.";
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(optMsg);
                    Console.ResetColor();
                    IterationLog.AppendLine(optMsg);
                    break; // Break the loop, we found the optimal answer!
                }

                // Figure out which row limits us the most so we don't break our constraints.
                int pivotRow = GetLeavingVariable(pivotCol);
                if (pivotRow == -1)
                {
                    // If we can't find a limit, the math proves the model can grow infinitely (Unbounded).
                    throw new InvalidOperationException("The model is UNBOUNDED. The pivot column contains no strictly positive technological coefficients.");
                }

                string pivotMsg = $"\n[PIVOT STEP] Entering: {ColumnHeaders[pivotCol]} | Leaving: {RowHeaders[pivotRow]}";
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(pivotMsg);
                Console.ResetColor();
                IterationLog.AppendLine(pivotMsg);

                // Do the actual math: normalize the row and zero-out the rest of the column.
                PerformPivot(pivotRow, pivotCol);
                iterationCount++;
                LogAndPrintTableau(iterationCount);
            }

            CheckFeasibility();
        }

        // Helper: Finds the most negative number in the Z-row.
        private int GetEnteringVariable()
        {
            int enteringCol = -1;
            double minValue = -1e-7; // Using a tiny tolerance to avoid computer floating-point errors

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

        // Helper: Performs the Minimum Ratio Test (RHS / Pivot Column Value).
        private int GetLeavingVariable(int pivotCol)
        {
            int leavingRow = -1;
            double minRatio = double.MaxValue;

            for (int i = 1; i < NumRows; i++)
            {
                double coefficient = Tableau[i, pivotCol];
                if (coefficient > 1e-7) // Only divide by positive numbers!
                {
                    double rhs = Tableau[i, NumCols - 1];
                    double ratio = rhs / coefficient;

                    if (ratio < minRatio)
                    {
                        minRatio = ratio;
                        leavingRow = i;
                    }
                }
            }
            return leavingRow;
        }

        // Helper: The core Jordan pivoting math that manipulates the 2D array.
        private void PerformPivot(int pivotRow, int pivotCol)
        {
            // Swap the label
            RowHeaders[pivotRow] = ColumnHeaders[pivotCol];
            double pivotValue = Tableau[pivotRow, pivotCol];

            // Normalize the pivot row (divide everything by the pivot value)
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

        // Helper: A safety check to make sure our math didn't drift into an impossible state.
        private void CheckFeasibility()
        {
            SimplexResult currentResult = GetResult();
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
                    throw new InvalidOperationException("The model is INFEASIBLE. The constraints yield an empty feasible region.");
                }
            }
        }

        // Helper: Packages the final result so other team members can safely use it.
        public SimplexResult GetResult()
        {
            SimplexResult result = new SimplexResult();
            result.OptimalZ = Tableau[0, NumCols - 1];
            result.FinalTableau = (double[,])Tableau.Clone(); // Deep cloning so nobody breaks our base matrix
            result.RowHeaders = (string[])RowHeaders.Clone();
            result.ColumnHeaders = (string[])ColumnHeaders.Clone();

            for (int j = 0; j < NumCols - 1; j++)
            {
                string varName = ColumnHeaders[j];
                int rowIndex = Array.IndexOf(RowHeaders, varName);

                if (rowIndex != -1) result.VariableValues[varName] = Tableau[rowIndex, NumCols - 1];
                else result.VariableValues[varName] = 0.0;
            }

            return result;
        }

        // Helper: Creates a beautiful, dynamic text grid of the current numbers and saves it.
        private void LogAndPrintTableau(int iteration)
        {
            // Measure how wide each column needs to be to fit the numbers perfectly.
            int bvColWidth = 6;
            for (int i = 0; i < NumRows; i++)
            {
                if (RowHeaders[i].Length + 2 > bvColWidth) bvColWidth = RowHeaders[i].Length + 2;
            }

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

            // Draw the top border using basic ASCII so it doesn't break in Notepad
            string separator = GenerateAsciiSeparator(bvColWidth, colWidths);
            Console.WriteLine(separator);
            sb.AppendLine(separator);

            // Draw the Header Row (x1, x2, s1, etc)
            StringBuilder headerRow = new StringBuilder();
            headerRow.Append(" |").Append(CenterText("B.V", bvColWidth));
            for (int j = 0; j < NumCols; j++) headerRow.Append("|").Append(CenterText(ColumnHeaders[j], colWidths[j]));
            headerRow.Append("|");

            Console.WriteLine(headerRow.ToString());
            sb.AppendLine(headerRow.ToString());

            Console.WriteLine(separator);
            sb.AppendLine(separator);

            // Draw the Data Rows
            for (int i = 0; i < NumRows; i++)
            {
                StringBuilder dataRow = new StringBuilder();
                dataRow.Append(" |").Append(CenterText(RowHeaders[i], bvColWidth));
                for (int j = 0; j < NumCols; j++)
                {
                    // Forcing 3 decimal points strictly per requirements
                    string val = Tableau[i, j].ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                    dataRow.Append("|").Append(val.PadLeft(colWidths[j] - 1)).Append(" ");
                }
                dataRow.Append("|");

                Console.WriteLine(dataRow.ToString());
                sb.AppendLine(dataRow.ToString());

                // Draw a line under the Z-Row
                if (i == 0 && NumRows > 1)
                {
                    Console.WriteLine(separator);
                    sb.AppendLine(separator);
                }
            }

            Console.WriteLine(separator);
            sb.AppendLine(separator);

            // Finally, save this grid to our log!
            IterationLog.Append(sb.ToString());
        }

        private string GenerateAsciiSeparator(int bvWidth, int[] colWidths)
        {
            StringBuilder sep = new StringBuilder();
            sep.Append(" +").Append(new string('-', bvWidth));
            for (int j = 0; j < colWidths.Length; j++) sep.Append("+").Append(new string('-', colWidths[j]));
            sep.Append("+");
            return sep.ToString();
        }

        private static string CenterText(string text, int width)
        {
            if (text.Length >= width) return text;
            int leftPadding = (width - text.Length) / 2;
            int rightPadding = width - text.Length - leftPadding;
            return new string(' ', leftPadding) + text + new string(' ', rightPadding);
        }
    }
}