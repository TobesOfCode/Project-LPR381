using System;
using System.Collections.Generic;
using System.Text;
using LPR381.Models;

namespace LPR381.Layer2_SolverEngine
{
    // -------------------------------------------------------------------------
    // CLASS: BaseSimplex (Layer 2 Solver Engine)
    // Purpose: The core mathematical engine of the project. It solves continuous 
    // Linear Programming (LP) problems using the standard Primal Simplex method.
    // What it does: It takes an objective (like maximizing profit) and constraints 
    // (like limited materials), and finds the absolute best numerical answer.
    // -------------------------------------------------------------------------
    public class BaseSimplex
    {
        // The main mathematical grid (matrix) where all the calculations happen.
        public double[,] Tableau { get; private set; }

        // Track the size of the grid.
        public int NumRows { get; private set; }
        public int NumCols { get; private set; }

        // Labels for the columns (like x1, x2, s1) and rows (like Z, c1, c2).
        public string[] ColumnHeaders { get; private set; }
        public string[] RowHeaders { get; private set; }

        // A running text log that records every table and step to save to a file later.
        public StringBuilder IterationLog { get; private set; } = new StringBuilder();

        // Remembers if we are trying to find the highest value (max) or lowest value (min).
        private bool isMaximization;
        // Keeps a safe copy of the original rules provided by the user.
        private LinearModel originalModel;

        // -------------------------------------------------------------------------
        // METHOD: InitializeTableau
        // Purpose: Prepares the math grid before we start calculating.
        // What it does: Converts normal inequalities (like <= or >=) into strict 
        // equalities (=) by adding "slack" or "excess" variables.
        // How it works: It counts how many variables we need, builds an empty grid, 
        // labels the columns and rows, and fills in the starting numbers.
        // -------------------------------------------------------------------------
        public void InitializeTableau(LinearModel model)
        {
            originalModel = model;
            isMaximization = model.OptimizationType == "max";

            // Step 1: Handle strict equalities (=) by breaking them into two rules (<= and >=)
            List<Constraint> processedConstraints = new List<Constraint>();
            foreach (var c in model.Constraints)
            {
                if (c.Relation == "=")
                {
                    processedConstraints.Add(new Constraint { Coefficients = new List<double>(c.Coefficients), Relation = "<=", RHS = c.RHS });
                    processedConstraints.Add(new Constraint { Coefficients = new List<double>(c.Coefficients), Relation = ">=", RHS = c.RHS });
                }
                else
                {
                    processedConstraints.Add(c);
                }
            }

            // Step 2: Count how many decision variables (x) and extra variables (s or e) we need.
            int numDecisionVars = model.ObjectiveCoefficients.Count;
            int numSlacks = 0;
            int numExcess = 0;

            foreach (var c in processedConstraints)
            {
                if (c.Relation == "<=") numSlacks++;
                else if (c.Relation == ">=") numExcess++;
            }

            // Step 3: Size the grid. +1 row for the Z-row (objective). +1 col for the RHS (answers).
            NumRows = processedConstraints.Count + 1;
            NumCols = numDecisionVars + numSlacks + numExcess + 1;

            Tableau = new double[NumRows, NumCols];
            ColumnHeaders = new string[NumCols];
            RowHeaders = new string[NumRows];

            // Step 4: Name the decision variable columns (x1, x2, etc.)
            for (int j = 0; j < numDecisionVars; j++) ColumnHeaders[j] = "x" + (j + 1);

            int currentSlack = numDecisionVars;
            int currentExcess = numDecisionVars + numSlacks;

            // Name the slack (s) and excess (e) columns
            int sIdx = 1, eIdx = 1;
            for (int j = 0; j < numSlacks; j++) ColumnHeaders[currentSlack + j] = "s" + (sIdx++);
            for (int j = 0; j < numExcess; j++) ColumnHeaders[currentExcess + j] = "e" + (eIdx++);

            ColumnHeaders[NumCols - 1] = "RHS";
            RowHeaders[0] = "Z";

            // Step 5: Fill in the top row (Z-Row). If maximizing, we flip the signs to negative.
            for (int j = 0; j < numDecisionVars; j++)
            {
                Tableau[0, j] = isMaximization ? -model.ObjectiveCoefficients[j] : model.ObjectiveCoefficients[j];
            }

            // Step 6: Fill in the rest of the grid with the constraint numbers.
            for (int i = 0; i < processedConstraints.Count; i++)
            {
                int rowIndex = i + 1;
                var con = processedConstraints[i];

                for (int j = 0; j < numDecisionVars; j++) Tableau[rowIndex, j] = con.Coefficients[j];
                Tableau[rowIndex, NumCols - 1] = con.RHS;

                // Put a '1' in the correct slack/excess column to balance the equation.
                if (con.Relation == "<=")
                {
                    Tableau[rowIndex, currentSlack] = 1.0;
                    RowHeaders[rowIndex] = ColumnHeaders[currentSlack];
                    currentSlack++;
                }
                else if (con.Relation == ">=")
                {
                    Tableau[rowIndex, currentExcess] = -1.0;
                    RowHeaders[rowIndex] = ColumnHeaders[currentExcess];

                    // For excess, we multiply the whole row by -1 to keep the math valid.
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
        // Purpose: The main brain of the algorithm.
        // What it does: Runs a loop that continually improves the answer until it's perfect.
        // How it works: It finds a variable to enter the solution, finds a variable to 
        // kick out, does the math (pivoting), and repeats until no more improvements can be made.
        // -------------------------------------------------------------------------
        public void Solve()
        {
            int iterationCount = 0;
            LogAndPrintTableau(iterationCount); // Show the starting table

            while (true)
            {
                // Safety net: stop if the math gets stuck in an endless loop.
                if (iterationCount > 1000) throw new Exception("Algorithm failed to converge (Infinite loop detected).");

                // Step 1: Who enters? Find the column that improves profit the most.
                int pivotCol = GetEnteringVariable();

                // If no column improves the profit, we are done! The current answer is optimal.
                if (pivotCol == -1)
                {
                    string optMsg = "\n[SYSTEM] Optimality criterion satisfied. No negative coefficients in Z-Row.";
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(optMsg);
                    Console.ResetColor();
                    IterationLog.AppendLine(optMsg);
                    break;
                }

                // Step 2: Who leaves? Find the row that hits its resource limit first.
                int pivotRow = GetLeavingVariable(pivotCol);

                // If no row stops the variable from growing, the problem is broken (infinite profit).
                if (pivotRow == -1)
                {
                    throw new InvalidOperationException("The model is UNBOUNDED. The pivot column contains no positive limit ratios.");
                }

                // Announce what we are swapping
                string pivotMsg = $"\n[PIVOT STEP] Entering: {ColumnHeaders[pivotCol]} | Leaving: {RowHeaders[pivotRow]}";
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(pivotMsg);
                Console.ResetColor();
                IterationLog.AppendLine(pivotMsg);

                // Step 3: Do the math to swap them!
                PerformPivot(pivotRow, pivotCol);
                iterationCount++;

                // Print the new grid
                LogAndPrintTableau(iterationCount);
            }

            // Finally, double check that our math didn't accidentally break any original rules.
            CheckFeasibility();
        }

        // -------------------------------------------------------------------------
        // METHOD: GetEnteringVariable
        // Purpose: Decides which column to focus on next.
        // How it works: Scans the top Z-Row. For maximization, it looks for the most 
        // negative number. That column represents the variable that increases profit the most.
        // -------------------------------------------------------------------------
        private int GetEnteringVariable()
        {
            int enteringCol = -1;
            double minValue = -1e-7; // Small tolerance to ignore computer rounding errors

            for (int j = 0; j < NumCols - 1; j++)
            {
                if (Tableau[0, j] < minValue)
                {
                    minValue = Tableau[0, j];
                    enteringCol = j;
                }
            }
            return enteringCol; // Returns -1 if no negative numbers are left
        }

        // -------------------------------------------------------------------------
        // METHOD: GetLeavingVariable (Minimum Ratio Test)
        // Purpose: Decides which row to swap out.
        // How it works: It divides the RHS answers by the numbers in our chosen column.
        // The smallest positive ratio tells us which constraint runs out of resources first.
        // -------------------------------------------------------------------------
        private int GetLeavingVariable(int pivotCol)
        {
            int leavingRow = -1;
            double minRatio = double.MaxValue;

            for (int i = 1; i < NumRows; i++)
            {
                double coefficient = Tableau[i, pivotCol];
                if (coefficient > 1e-7) // We only divide by positive numbers
                {
                    double rhs = Tableau[i, NumCols - 1];
                    double ratio = rhs / coefficient;

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
        // Purpose: The actual number-crunching step (Gauss-Jordan Elimination).
        // What it does: Updates the grid so the entering variable becomes active.
        // How it works: It divides the chosen row so the intersection (pivot) becomes 1. 
        // Then it uses that row to turn every other number in the chosen column into 0.
        // -------------------------------------------------------------------------
        private void PerformPivot(int pivotRow, int pivotCol)
        {
            // Update the label on the side of the table
            RowHeaders[pivotRow] = ColumnHeaders[pivotCol];
            double pivotValue = Tableau[pivotRow, pivotCol];

            // Make the pivot value exactly 1
            for (int j = 0; j < NumCols; j++) Tableau[pivotRow, j] /= pivotValue;

            // Make all other numbers in that column exactly 0
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
        // Purpose: A final safety check.
        // What it does: Plugs our final answers back into the user's original equations.
        // How it works: If the final answer says x1=5, it tests if x1=5 actually obeys 
        // the rule "x1 <= 4". If it doesn't, it throws an error to stop the program.
        // -------------------------------------------------------------------------
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
        // Purpose: Packages the final data to be shared with other classes.
        // What it does: Bundles the highest profit (Z), the final grid, and the answers 
        // into a neat SimplexResult object.
        // -------------------------------------------------------------------------
        public SimplexResult GetResult()
        {
            SimplexResult result = new SimplexResult();
            // The ultimate answer (Z) is found in the top right corner of the grid.
            result.OptimalZ = Tableau[0, NumCols - 1];

            result.FinalTableau = (double[,])Tableau.Clone();
            result.RowHeaders = (string[])RowHeaders.Clone();
            result.ColumnHeaders = (string[])ColumnHeaders.Clone();

            // Match column headers to row headers to figure out what variables equal what number.
            for (int j = 0; j < NumCols - 1; j++)
            {
                string varName = ColumnHeaders[j];
                int rowIndex = Array.IndexOf(RowHeaders, varName);

                if (rowIndex != -1) result.VariableValues[varName] = Tableau[rowIndex, NumCols - 1];
                else result.VariableValues[varName] = 0.0; // If it's not basic, it equals 0.
            }

            return result;
        }

        // -------------------------------------------------------------------------
        // UNIFORM TABLE FORMATTER: LogAndPrintTableau
        // Purpose: Draws the mathematical grid on the screen.
        // What it does: Creates a neat ASCII box (+------+--------+) to show the math clearly.
        // How it works: Uses loops and string formatting to force perfect vertical 
        // alignment. It explicitly names the constraint rows c1, c2, c3 as requested.
        // -------------------------------------------------------------------------
        private void LogAndPrintTableau(int iteration)
        {
            string title = iteration == 0 ? "Initial Tableau (Canonical Form)" : $"Tableau Iteration {iteration}";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n--- {title} ---");
            Console.ResetColor();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"\n--- {title} ---");

            // Draw the top border line
            sb.Append("+------+");
            for (int c = 0; c < ColumnHeaders.Length; c++) sb.Append("--------+");
            sb.AppendLine();

            // Draw the column names (x1, x2, s1, etc.)
            sb.Append("| B.V  |");
            foreach (var header in ColumnHeaders)
            {
                sb.Append($" {header,6} |");
            }
            sb.AppendLine();

            // Draw the line under the headers
            sb.Append("+------+");
            for (int c = 0; c < ColumnHeaders.Length; c++) sb.Append("--------+");
            sb.AppendLine();

            // Loop through and draw the actual numbers row by row
            for (int i = 0; i < NumRows; i++)
            {
                // Force the labels on the left to show "Z" for row 0, and "c1", "c2" for constraints.
                string bvLabel = (i == 0) ? "Z" : $"c{i}";
                sb.Append($"| {bvLabel,-4} |");

                for (int j = 0; j < NumCols; j++)
                {
                    double val = Tableau[i, j];

                    // Clean up ugly floating point rounding errors (turns "-0.000" into "0.000")
                    if (Math.Abs(val) < 1e-7) val = 0.0;

                    // Pad the number to fit perfectly in 6 spaces with 3 decimal places
                    sb.Append($" {val,6:F3} |");
                }
                sb.AppendLine();

                // Draw a divider line right underneath the Z-Row to separate it from constraints.
                if (i == 0 && NumRows > 1)
                {
                    sb.Append("+------+");
                    for (int c = 0; c < ColumnHeaders.Length; c++) sb.Append("--------+");
                    sb.AppendLine();
                }
            }

            // Draw the final bottom border line
            sb.Append("+------+");
            for (int c = 0; c < ColumnHeaders.Length; c++) sb.Append("--------+");
            sb.AppendLine();

            string tableStr = sb.ToString();

            // Print it to the screen and save it to the log for exporting
            Console.WriteLine(tableStr);
            IterationLog.Append(tableStr);
        }
    }
}