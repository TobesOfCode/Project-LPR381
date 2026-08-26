using System;
using System.Collections.Generic;
using System.Text;
using LPR381.Models;

namespace LPR381.Layer2_SolverEngine
{
    // -------------------------------------------------------------------------
    // CLASS: CuttingPlane (Layer 2 Solver Engine)
    // Purpose: Solves Integer/Mixed-Integer Programming problems using the 
    // Gomory Cutting Plane algorithm paired with Dual Simplex, logging every table.
    // -------------------------------------------------------------------------
    internal class CuttingPlane
    {
        // Stores the complete trace of every iteration and table for the text export report.
        public StringBuilder IterationLog { get; private set; } = new StringBuilder();

        // -------------------------------------------------------------------------
        // METHOD: Solve
        // Purpose: Main entry point. Runs the initial continuous LP relaxation, 
        // then iteratively derives Gomory cuts and resolves via Dual Simplex.
        // -------------------------------------------------------------------------
        public SimplexResult Solve(LinearModel model)
        {
            IterationLog.Clear();
            IterationLog.AppendLine("\n==============================================================");
            IterationLog.AppendLine("             GOMORY CUTTING PLANE ALGORITHM                   ");
            IterationLog.AppendLine("==============================================================");

            // Step 1: Safely clone the model to inject necessary binary upper bounds
            LinearModel safeModel = CloneModel(model);

            // Crucial Fix: If variables are strictly BINARY, we MUST add x <= 1 constraints.
            for (int i = 0; i < safeModel.SignRestrictions.Count; i++)
            {
                if (safeModel.SignRestrictions[i].Trim().ToLower() == "bin")
                {
                    List<double> coeffs = new List<double>(new double[safeModel.ObjectiveCoefficients.Count]);
                    coeffs[i] = 1.0;
                    safeModel.Constraints.Add(new Constraint { Coefficients = coeffs, Relation = "<=", RHS = 1.0 });
                }
            }

            // Step 2: Run the initial continuous LP relaxation using BaseSimplex.
            BaseSimplex baseSolver = new BaseSimplex();
            baseSolver.InitializeTableau(safeModel);
            baseSolver.Solve();

            // Pull the resulting solved tableau, headers, and iteration log from BaseSimplex.
            SimplexResult currentResult = baseSolver.GetResult();
            double[,] currentTableau = (double[,])currentResult.FinalTableau.Clone();
            List<string> dynamicColHeaders = new List<string>(currentResult.ColumnHeaders);

            IterationLog.Append(baseSolver.IterationLog.ToString());

            int cutsApplied = 0;

            // Step 3: Loop to continuously apply cuts until no fractional RHS values remain.
            while (true)
            {
                int targetRow = FindFractionalBasicRow(currentTableau);

                if (targetRow == -1)
                {
                    string completeMsg = "\n[SYSTEM] No further fractional values found. Optimal integer solution reached.";
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(completeMsg);
                    Console.ResetColor();
                    IterationLog.AppendLine(completeMsg);
                    break;
                }

                cutsApplied++;

                // Build the header list for this cut UP FRONT, continuing the existing
                // s1, s2, s3... slack numbering sequence for the Gomory cut constraint.
                int nextSlackIndex = GetNextSlackIndex(dynamicColHeaders);
                List<string> headersForThisCut = new List<string>(dynamicColHeaders);
                headersForThisCut.Insert(headersForThisCut.Count - 1, $"s{nextSlackIndex}");

                bool cutApplied;
                double[,] expandedTableau = ApplyGomoryCutAndSolve(currentTableau, headersForThisCut, out cutApplied, cutsApplied, targetRow);

                if (!cutApplied) break;

                string cutMsg = $"\n[CUTTING PLANE] Gomory cut c{cutsApplied} generated and added as a new constraint. Dual Simplex executed.";
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(cutMsg);
                Console.ResetColor();
                IterationLog.AppendLine(cutMsg);

                // Commit the headers used for this cut as the new running header list.
                dynamicColHeaders = headersForThisCut;
                currentTableau = expandedTableau;

                // Safety guard to prevent infinite looping
                if (cutsApplied > 50)
                {
                    IterationLog.AppendLine("\n[!] Maximum safety limit of 50 cuts reached. Aborting loop.");
                    break;
                }
            }

            // Step 4: Package the final results into a standard SimplexResult care package.
            SimplexResult optimalState = new SimplexResult();
            optimalState.OptimalZ = currentTableau[0, currentTableau.GetLength(1) - 1];
            optimalState.FinalTableau = currentTableau;
            optimalState.ColumnHeaders = dynamicColHeaders.ToArray();

            // >>> FIX: Build and capture the RowHeaders array so Sensitivity Analysis doesn't crash <<<
            string[] finalRowHeaders = new string[currentTableau.GetLength(0)];
            finalRowHeaders[0] = "Z";
            for (int r = 1; r < currentTableau.GetLength(0); r++)
            {
                finalRowHeaders[r] = GetRowBasisLabel(currentTableau, dynamicColHeaders, r);
            }
            optimalState.RowHeaders = finalRowHeaders;
            // >>> END OF FIX <<<

            // Decipher the final variable values from the final tableau matrix.
            optimalState.VariableValues = ExtractVariablesFromTableau(currentTableau, dynamicColHeaders);

            IterationLog.AppendLine("\n==============================================================");
            IterationLog.AppendLine("                  GOMORY CUTTING PLANE SUMMARY                ");
            IterationLog.AppendLine("==============================================================");
            IterationLog.AppendLine($" Total Gomory Cuts Applied: {cutsApplied}");
            IterationLog.AppendLine($" Optimal Objective Value (Z): {optimalState.OptimalZ:F3}");
            IterationLog.AppendLine("==============================================================");

            return optimalState;
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: CloneModel
        // -------------------------------------------------------------------------
        private LinearModel CloneModel(LinearModel source)
        {
            LinearModel clone = new LinearModel
            {
                OptimizationType = source.OptimizationType,
                ObjectiveCoefficients = new List<double>(source.ObjectiveCoefficients),
                Constraints = new List<Constraint>(),
                SignRestrictions = new List<string>(source.SignRestrictions)
            };
            foreach (var c in source.Constraints)
            {
                clone.Constraints.Add(new Constraint { Coefficients = new List<double>(c.Coefficients), Relation = c.Relation, RHS = c.RHS });
            }
            return clone;
        }

        // -------------------------------------------------------------------------
        // METHOD: FindFractionalBasicRow
        // -------------------------------------------------------------------------
        private int FindFractionalBasicRow(double[,] tableau)
        {
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);
            int targetRow = -1;
            double largestFraction = 0.0;

            for (int r = 1; r < rows; r++)
            {
                double rhsValue = Math.Round(tableau[r, cols - 1], 6);
                double fraction = Math.Round(rhsValue - Math.Floor(rhsValue), 6);

                if (fraction > 0.0001 && fraction < 0.9999)
                {
                    if (fraction > largestFraction)
                    {
                        largestFraction = fraction;
                        targetRow = r;
                    }
                }
            }
            return targetRow;
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: GetNextSlackIndex
        // -------------------------------------------------------------------------
        private int GetNextSlackIndex(List<string> colHeaders)
        {
            int maxIndex = 0;
            foreach (var header in colHeaders)
            {
                if (header.Length > 1 && header[0] == 's' && int.TryParse(header.Substring(1), out int idx))
                {
                    if (idx > maxIndex) maxIndex = idx;
                }
            }
            return maxIndex + 1;
        }

        // -------------------------------------------------------------------------
        // METHOD: ApplyGomoryCutAndSolve
        // -------------------------------------------------------------------------
        private double[,] ApplyGomoryCutAndSolve(double[,] finalTableau, List<string> colHeaders, out bool cutApplied, int cutNum, int targetRow)
        {
            int rows = finalTableau.GetLength(0);
            int cols = finalTableau.GetLength(1);

            double rhsValue = Math.Round(finalTableau[targetRow, cols - 1], 6);
            double largestFraction = Math.Round(rhsValue - Math.Floor(rhsValue), 6);

            string cutInfo = $"\n--- Generating Gomory cut c{cutNum} (new constraint) from Row {targetRow} (RHS fractional part: {largestFraction:F3}) ---";
            Console.WriteLine(cutInfo);
            IterationLog.AppendLine(cutInfo);

            double[,] expandedTableau = new double[rows + 1, cols + 1];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols - 1; c++)
                {
                    expandedTableau[r, c] = finalTableau[r, c];
                }
                expandedTableau[r, cols] = finalTableau[r, cols - 1]; // Shift RHS to the end
            }

            int newRow = rows;
            int newSlackCol = cols - 1;

            // Generate fractions exactly according to Gomory Fractional Cut logic
            for (int c = 0; c < cols - 1; c++)
            {
                double coef = Math.Round(finalTableau[targetRow, c], 6);
                double fracCoef = Math.Round(coef - Math.Floor(coef), 6);
                if (Math.Abs(fracCoef - 1.0) < 1e-5) fracCoef = 0; // Fixes .99999 edge case
                expandedTableau[newRow, c] = -fracCoef;
            }

            expandedTableau[newRow, newSlackCol] = 1.0;
            double rhsFrac = Math.Round(rhsValue - Math.Floor(rhsValue), 6);
            expandedTableau[newRow, cols] = -rhsFrac;

            AppendTableauToLog(expandedTableau, colHeaders, $"Tableau with Gomory Cut c{cutNum} Added (Pre-Dual Simplex)");
            RunDualSimplex(expandedTableau, colHeaders, cutNum);

            cutApplied = true;
            return expandedTableau;
        }

        // -------------------------------------------------------------------------
        // METHOD: RunDualSimplex
        // -------------------------------------------------------------------------
        private void RunDualSimplex(double[,] tableau, List<string> colHeaders, int cutNum)
        {
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);
            int dualIteration = 0;

            while (true)
            {
                dualIteration++;
                int pivotRow = -1;
                double minRhs = -0.0001;

                for (int r = 1; r < rows; r++)
                {
                    double rhs = tableau[r, cols - 1];
                    if (rhs < minRhs)
                    {
                        minRhs = rhs;
                        pivotRow = r;
                    }
                }

                if (pivotRow == -1) break;

                int pivotCol = -1;
                double minRatio = double.MaxValue;

                for (int c = 0; c < cols - 1; c++)
                {
                    double rowVal = tableau[pivotRow, c];
                    if (rowVal < -0.0001)
                    {
                        double objVal = tableau[0, c];
                        double ratio = Math.Abs(objVal / rowVal);
                        if (ratio < minRatio)
                        {
                            minRatio = ratio;
                            pivotCol = c;
                        }
                    }
                }

                if (pivotCol == -1) throw new InvalidOperationException("Dual Simplex failure: Model is infeasible.");

                string dualStepMsg = $"[DUAL PIVOT - Cut c{cutNum}, Step {dualIteration}] Entering: {colHeaders[pivotCol]} | Leaving Row: {pivotRow}";
                Console.WriteLine($"  {dualStepMsg}");
                IterationLog.AppendLine($"  {dualStepMsg}");

                double pivotVal = tableau[pivotRow, pivotCol];
                for (int c = 0; c < cols; c++) tableau[pivotRow, c] /= pivotVal;

                for (int r = 0; r < rows; r++)
                {
                    if (r != pivotRow)
                    {
                        double factor = tableau[r, pivotCol];
                        for (int c = 0; c < cols; c++) tableau[r, c] -= factor * tableau[pivotRow, c];
                    }
                }

                AppendTableauToLog(tableau, colHeaders, $"Gomory Cut c{cutNum} - Dual Simplex Iteration {dualIteration}");
            }
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: GetRowBasisLabel
        // -------------------------------------------------------------------------
        private string GetRowBasisLabel(double[,] tableau, List<string> colHeaders, int r)
        {
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);

            for (int c = 0; c < cols - 1; c++)
            {
                if (Math.Abs(tableau[r, c] - 1.0) < 1e-4)
                {
                    bool isUnit = true;
                    for (int checkR = 0; checkR < rows; checkR++)
                    {
                        if (checkR != r && Math.Abs(tableau[checkR, c]) > 1e-4)
                        {
                            isUnit = false;
                            break;
                        }
                    }
                    if (isUnit && c < colHeaders.Count)
                    {
                        return colHeaders[c];
                    }
                }
            }
            return $"s{r}"; // Fallback
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: AppendTableauToLog (UNIVERSAL FORMATTER)
        // -------------------------------------------------------------------------
        private void AppendTableauToLog(double[,] tableau, List<string> colHeaders, string tableName)
        {
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"\n--- {tableName} ---");

            sb.Append("+------+");
            for (int c = 0; c < cols; c++) sb.Append("--------+");
            sb.AppendLine();

            sb.Append("| B.V  |");
            for (int c = 0; c < cols - 1; c++)
            {
                sb.Append($" {colHeaders[c],6} |");
            }
            sb.Append($" {"RHS",6} |\n");

            sb.Append("+------+");
            for (int c = 0; c < cols; c++) sb.Append("--------+");
            sb.AppendLine();

            for (int r = 0; r < rows; r++)
            {
                string bvLabel = (r == 0) ? "Z" : $"c{r}";

                for (int c = 0; r > 0 && c < cols - 1; c++)
                {
                    if (Math.Abs(tableau[r, c] - 1.0) < 1e-4)
                    {
                        bool isUnit = true;
                        for (int checkR = 0; checkR < rows; checkR++)
                        {
                            if (checkR != r && Math.Abs(tableau[checkR, c]) > 1e-4) { isUnit = false; break; }
                        }
                        if (isUnit) { bvLabel = colHeaders[c]; break; }
                    }
                }

                sb.Append($"| {bvLabel,-4} |");

                for (int c = 0; c < cols; c++)
                {
                    double val = tableau[r, c];
                    if (Math.Abs(val) < 1e-7) val = 0.0;

                    sb.Append($" {val,6:F3} |");
                }
                sb.AppendLine();
            }

            sb.Append("+------+");
            for (int c = 0; c < cols; c++) sb.Append("--------+");
            sb.AppendLine();

            string tableStr = sb.ToString();
            Console.Write(tableStr);
            IterationLog.Append(tableStr);
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: ExtractVariablesFromTableau
        // -------------------------------------------------------------------------
        private Dictionary<string, double> ExtractVariablesFromTableau(double[,] tableau, List<string> colHeaders)
        {
            var results = new Dictionary<string, double>();
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);

            for (int c = 0; c < cols - 1; c++)
            {
                string varName = colHeaders[c];
                int oneCount = 0;
                int zeroCount = 0;
                int oneRowIndex = -1;

                for (int r = 0; r < rows; r++)
                {
                    if (Math.Abs(tableau[r, c] - 1.0) < 0.0001)
                    {
                        oneCount++;
                        oneRowIndex = r;
                    }
                    else if (Math.Abs(tableau[r, c]) < 0.0001)
                    {
                        zeroCount++;
                    }
                }

                if (oneCount == 1 && zeroCount == rows - 1 && oneRowIndex > 0)
                {
                    results[varName] = Math.Round(tableau[oneRowIndex, cols - 1], 5);
                }
                else
                {
                    results[varName] = 0.0;
                }
            }
            return results;
        }
    }
}