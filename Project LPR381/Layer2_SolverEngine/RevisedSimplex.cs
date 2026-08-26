using System;
using System.Collections.Generic;
using System.Text;
using LPR381.Models;

namespace LPR381.Layer2_SolverEngine
{
    // Layer 2: The Revised Solver Engine.
    // This is the memory-efficient solver. Instead of carrying around a giant spreadsheet of numbers and updating every single cell,
    // it only updates a specific "Inverse Basis Matrix". It then mathematically calculates only the specific pieces it needs on-the-fly.
    public class RevisedSimplex
    {
        // This keeps a text record of everything the algorithm does so we can print it out later.
        public StringBuilder IterationLog { get; private set; } = new StringBuilder();

        // Safe copies of our original problem so we know if we are maximizing or minimizing.
        private LinearModel originalModel;
        private bool isMaximization;

        // -------------------------------------------------------------------------
        // THE BUILDING BLOCKS (Matrix Memory)
        // -------------------------------------------------------------------------
        private double[,] A;       // The original constraint numbers (left side of the equations).
        private double[] c;        // The original objective numbers (profits/costs).
        private double[] b;        // The RHS numbers (capacities/limits).
        private double[,] B_inv;   // The magic "Inverse Basis Matrix" that tracks all changes mathematically.
        private int[] BV;          // Keeps track of which variables are currently "Basic" (active in our solution).

        private int NumRows;
        private int NumCols;
        private string[] VariableNames; // Holds the names like "x1", "s1", "e1"

        // -------------------------------------------------------------------------
        // METHOD: InitializeModel
        // Purpose: Sets up all the matrices and arrays before the math begins.
        // How it works: It splits "=" rules into "<=" and ">=", assigns Slack to "<=", 
        // and Excess to ">=". No artificial variables allowed!
        // -------------------------------------------------------------------------
        public void InitializeModel(LinearModel model)
        {
            originalModel = model;
            isMaximization = model.OptimizationType == "max";

            // Step 1: Pre-process constraints. We do not use Big-M. 
            // If we see an exact equal (=) rule, we literally split it into two separate rules.
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

            int numDecisionVars = model.ObjectiveCoefficients.Count;
            int numSlacks = 0;
            int numExcess = 0;

            // Step 2: Count how many of each extra variable we need based on the rules.
            foreach (var con in processedConstraints)
            {
                if (con.Relation == "<=") numSlacks++;
                else if (con.Relation == ">=") numExcess++;
            }

            // Set the exact sizes of our arrays
            NumRows = processedConstraints.Count;
            NumCols = numDecisionVars + numSlacks + numExcess;

            A = new double[NumRows, NumCols];
            c = new double[NumCols];
            b = new double[NumRows];
            B_inv = new double[NumRows, NumRows];
            BV = new int[NumRows];
            VariableNames = new string[NumCols];

            // Load the objective function into our 'c' array
            for (int j = 0; j < numDecisionVars; j++)
            {
                VariableNames[j] = "x" + (j + 1);
                c[j] = isMaximization ? model.ObjectiveCoefficients[j] : -model.ObjectiveCoefficients[j];
            }

            int currentSlack = numDecisionVars;
            int currentExcess = numDecisionVars + numSlacks;

            // Pre-name our extra variables so we can track them easily later
            int sIdx = 1, eIdx = 1;
            for (int j = 0; j < numSlacks; j++) VariableNames[currentSlack + j] = "s" + (sIdx++);
            for (int j = 0; j < numExcess; j++) VariableNames[currentExcess + j] = "e" + (eIdx++);

            // Build our starting matrices using strictly pure Primal Slack & Excess rules
            for (int i = 0; i < NumRows; i++)
            {
                var con = processedConstraints[i];
                for (int j = 0; j < numDecisionVars; j++)
                {
                    A[i, j] = con.Coefficients[j];
                }
                b[i] = con.RHS;

                if (con.Relation == "<=")
                {
                    A[i, currentSlack] = 1.0;
                    c[currentSlack] = 0.0;
                    BV[i] = currentSlack;
                    currentSlack++;
                    B_inv[i, i] = 1.0; // Slack establishes a normal Identity Matrix row
                }
                else if (con.Relation == ">=")
                {
                    A[i, currentExcess] = -1.0;
                    c[currentExcess] = 0.0;
                    BV[i] = currentExcess;
                    currentExcess++;

                    // To set the excess variable as basic, its coefficient must logically be +1 in the basis.
                    // We achieve this natively in Revised Simplex by setting the Inverse Basis Matrix diagonal to -1.
                    // This creates a mathematical inversion (like multiplying the entire row by -1).
                    B_inv[i, i] = -1.0;
                }
            }
        }

        // -------------------------------------------------------------------------
        // METHOD: Solve
        // Purpose: The main engine loop. It uses Matrix Algebra to find the best answer.
        // -------------------------------------------------------------------------
        public void Solve()
        {
            int iteration = 0;
            IterationLog.AppendLine("\n==============================================================");
            IterationLog.AppendLine("         REVISED PRIMAL SIMPLEX INITIALIZED");
            IterationLog.AppendLine("==============================================================");

            while (true)
            {
                iteration++;
                // Safety net: kill switch for infinite loops
                if (iteration > 1000) throw new Exception("Algorithm failed to converge (Infinite loop detected).");

                string header = $"\n--- ITERATION {iteration} ---";
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(header);
                Console.ResetColor();
                IterationLog.AppendLine(header);

                // Step 1: Calculate where we currently stand by multiplying our Inverse Basis by the capacities (RHS).
                double[] x_B = MultiplyMatrixVector(B_inv, b);
                LogBasicVariables(x_B);

                // --- PRICE OUT PHASE ---
                // Calculate "Pi" (shadow prices). This tells us the current opportunity cost of our variables.
                double[] c_B = GetCb();
                double[] pi = MultiplyVectorMatrix(c_B, B_inv);

                IterationLog.AppendLine("\n[PRICE OUT PHASE]");
                IterationLog.AppendLine($"Simplex Multipliers (Pi): [{FormatArray(pi)}]");

                int enteringCol = -1;
                double minReducedCost = -1e-7;

                // Test every variable that isn't currently in our solution to see if adding it makes the objective better.
                for (int j = 0; j < NumCols; j++)
                {
                    if (Array.IndexOf(BV, j) != -1) continue;

                    double[] A_j = GetColumn(A, j);
                    double zj = DotProduct(pi, A_j);
                    double reducedCost = zj - c[j]; // This is the Z-row value!

                    // We want the most negative reduced cost
                    if (reducedCost < minReducedCost)
                    {
                        minReducedCost = reducedCost;
                        enteringCol = j;
                    }
                }

                // If no negative reduced costs are left, we found the absolute best answer!
                if (enteringCol == -1)
                {
                    string optMsg = "\n[SYSTEM] Optimality criterion satisfied. All reduced costs are >= 0.";
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(optMsg);
                    Console.ResetColor();
                    IterationLog.AppendLine(optMsg);
                    break;
                }

                string enterCostStr = minReducedCost.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                string enterMsg = $"Entering Variable: {VariableNames[enteringCol]} (Reduced Cost: {enterCostStr})";
                Console.WriteLine($"  {enterMsg}");
                IterationLog.AppendLine(enterMsg);

                // --- PRODUCT FORM PHASE ---
                // Calculate just the specific column we need to figure out what variable has to leave.
                // We do this by multiplying our Inverse Matrix by the original column.
                IterationLog.AppendLine("\n[PRODUCT FORM PHASE]");
                double[] entering_A_j = GetColumn(A, enteringCol);
                double[] d = MultiplyMatrixVector(B_inv, entering_A_j);
                IterationLog.AppendLine($"Computed Column (d = B^-1 * A_j): [{FormatArray(d)}]");

                int leavingRow = -1;
                double minRatio = double.MaxValue;

                // The Ratio Test: Find the tightest limit so we don't break our boundaries.
                for (int i = 0; i < NumRows; i++)
                {
                    if (d[i] > 1e-7) // Only divide by positive numbers
                    {
                        double ratio = x_B[i] / d[i];

                        // Primal Simplex strictly requires non-negative ratios.
                        if (ratio >= 0 && ratio < minRatio)
                        {
                            minRatio = ratio;
                            leavingRow = i;
                        }
                    }
                }

                // If there are no limits, the math proves the model can grow infinitely (broken).
                if (leavingRow == -1)
                {
                    throw new InvalidOperationException("The model is UNBOUNDED. The computed entering column has no positive limit ratios.");
                }

                string leaveRatioStr = minRatio.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                string leaveMsg = $"Leaving Variable: {VariableNames[BV[leavingRow]]} (Row {leavingRow + 1}, Min Ratio: {leaveRatioStr})";
                Console.WriteLine($"  {leaveMsg}");
                IterationLog.AppendLine(leaveMsg);

                // Create the Eta Matrix (E). This is a mathematical trick to instantly update our 
                // Inverse Basis Matrix with the new changes without recalculating everything.
                double[,] E = CreateIdentityMatrix(NumRows);
                for (int i = 0; i < NumRows; i++)
                {
                    if (i == leavingRow)
                        E[i, leavingRow] = 1.0 / d[leavingRow];
                    else
                        E[i, leavingRow] = -d[i] / d[leavingRow];
                }

                // Apply the Eta Matrix to our tracking matrix.
                B_inv = MultiplyMatrixMatrix(E, B_inv);

                // Swap the variable out of our active tracking list.
                BV[leavingRow] = enteringCol;
            }

            CheckFeasibility();
        }

        // -------------------------------------------------------------------------
        // METHOD: GetResult
        // Purpose: Builds the standard SimplexResult care package.
        // How it works: Even though we used matrix math, Layer 3 (Exporter) and the other 
        // team members expect a full 2D array grid. This method mathematically rebuilds 
        // the entire final grid so nobody downstream knows the difference!
        // -------------------------------------------------------------------------
        public SimplexResult GetResult()
        {
            SimplexResult result = new SimplexResult();
            result.FinalTableau = new double[NumRows + 1, NumCols + 1];
            result.ColumnHeaders = new string[NumCols + 1];
            result.RowHeaders = new string[NumRows + 1];

            result.RowHeaders[0] = "Z";
            for (int j = 0; j < NumCols; j++) result.ColumnHeaders[j] = VariableNames[j];
            result.ColumnHeaders[NumCols] = "RHS";

            double[] c_B = GetCb();
            double[] pi = MultiplyVectorMatrix(c_B, B_inv);
            double[] b_bar = MultiplyMatrixVector(B_inv, b);

            // Rebuild the Z-row
            for (int j = 0; j < NumCols; j++)
            {
                double zj = DotProduct(pi, GetColumn(A, j));
                result.FinalTableau[0, j] = zj - c[j];
            }
            result.FinalTableau[0, NumCols] = DotProduct(pi, b);
            result.OptimalZ = result.FinalTableau[0, NumCols];

            // Rebuild the body of the grid
            for (int i = 0; i < NumRows; i++)
            {
                result.RowHeaders[i + 1] = VariableNames[BV[i]];

                for (int j = 0; j < NumCols; j++)
                {
                    double[] d = MultiplyMatrixVector(B_inv, GetColumn(A, j));
                    result.FinalTableau[i + 1, j] = d[i];
                }
                result.FinalTableau[i + 1, NumCols] = b_bar[i];

                // Map the final answers directly for the dictionary
                result.VariableValues[VariableNames[BV[i]]] = b_bar[i];
            }

            // Ensure unused variables are explicitly marked as 0.0
            for (int j = 0; j < NumCols; j++)
            {
                if (Array.IndexOf(BV, j) == -1) result.VariableValues[VariableNames[j]] = 0.0;
            }

            return result;
        }

        // -------------------------------------------------------------------------
        // METHOD: CheckFeasibility
        // Purpose: Double-checks the final answer to make sure the math didn't lie to us.
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
        // CORE MATH HELPERS
        // These are standard linear algebra functions that handle all the row and 
        // column multiplications needed to keep the engine running smoothly.
        // -------------------------------------------------------------------------

        // Prints the current active variables to our text log
        private void LogBasicVariables(double[] x_B)
        {
            IterationLog.Append("Current Basic Variables: ");
            for (int i = 0; i < NumRows; i++)
            {
                string valStr = x_B[i].ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                IterationLog.Append($"{VariableNames[BV[i]]} = {valStr} | ");
            }
            IterationLog.AppendLine();
        }

        // Pulls out the objective function values for just the variables currently in our solution
        private double[] GetCb()
        {
            double[] cb = new double[NumRows];
            for (int i = 0; i < NumRows; i++) cb[i] = c[BV[i]];
            return cb;
        }

        // Extracts a single vertical column from a 2D matrix
        private double[] GetColumn(double[,] matrix, int colIndex)
        {
            double[] col = new double[NumRows];
            for (int i = 0; i < NumRows; i++) col[i] = matrix[i, colIndex];
            return col;
        }

        // Multiplies two arrays together and adds up the result
        private double DotProduct(double[] v1, double[] v2)
        {
            double sum = 0;
            for (int i = 0; i < v1.Length; i++) sum += v1[i] * v2[i];
            return sum;
        }

        // Multiplies a 1D array by a 2D matrix
        private double[] MultiplyVectorMatrix(double[] v, double[,] m)
        {
            double[] result = new double[m.GetLength(1)];
            for (int j = 0; j < m.GetLength(1); j++)
                for (int i = 0; i < v.Length; i++)
                    result[j] += v[i] * m[i, j];
            return result;
        }

        // Multiplies a 2D matrix by a 1D array
        private double[] MultiplyMatrixVector(double[,] m, double[] v)
        {
            double[] result = new double[m.GetLength(0)];
            for (int i = 0; i < m.GetLength(0); i++)
                for (int j = 0; j < v.Length; j++)
                    result[i] += m[i, j] * v[j];
            return result;
        }

        // Multiplies two 2D matrices together
        private double[,] MultiplyMatrixMatrix(double[,] m1, double[,] m2)
        {
            int r1 = m1.GetLength(0), c1 = m1.GetLength(1), c2 = m2.GetLength(1);
            double[,] result = new double[r1, c2];
            for (int i = 0; i < r1; i++)
                for (int j = 0; j < c2; j++)
                    for (int k = 0; k < c1; k++)
                        result[i, j] += m1[i, k] * m2[k, j];
            return result;
        }

        // Creates a starting matrix with 1s diagonally and 0s everywhere else
        private double[,] CreateIdentityMatrix(int size)
        {
            double[,] identity = new double[size, size];
            for (int i = 0; i < size; i++) identity[i, i] = 1.0;
            return identity;
        }

        // Safely converts an array of numbers into a formatted string separated by commas
        private string FormatArray(double[] array)
        {
            string[] formatted = new string[array.Length];
            for (int i = 0; i < array.Length; i++) formatted[i] = array[i].ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            return string.Join(", ", formatted);
        }
    }
}