// File: Layer2_SolverEngine/RevisedSimplex.cs
using System;
using System.Text;
using LPR381.Models;

namespace LPR381.Layer2_SolverEngine
{
    // Layer 2: The Revised Solver Engine.
    // Instead of doing math on a giant 2D grid every step, this algorithm only updates an "Inverse Basis Matrix".
    // It calculates the specific rows and columns it needs on-the-fly using "Pricing Out" and "Product Form".
    public class RevisedSimplex
    {
        public StringBuilder IterationLog { get; private set; } = new StringBuilder();

        private LinearModel originalModel;
        private bool isMaximization;

        // Mathematical structures for the Revised Simplex
        private double[,] A;       // The original constraint coefficients
        private double[] c;        // The original objective coefficients
        private double[] b;        // The right-hand side values (RHS)
        private double[,] B_inv;   // The mighty Inverse Basis Matrix (B^-1)
        private int[] BV;          // Keeps track of which variables are currently Basic

        private int NumRows;
        private int NumCols;
        private string[] VariableNames;

        // Step 1: Initialize the arrays instead of a standard tableau
        public void InitializeModel(LinearModel model)
        {
            originalModel = model;
            isMaximization = model.OptimizationType == "max";

            int numDecVars = model.ObjectiveCoefficients.Count;
            int numSlackVars = model.Constraints.Count;
            NumRows = model.Constraints.Count;
            NumCols = numDecVars + numSlackVars;

            A = new double[NumRows, NumCols];
            c = new double[NumCols];
            b = new double[NumRows];
            B_inv = new double[NumRows, NumRows];
            BV = new int[NumRows];
            VariableNames = new string[NumCols];

            // 1. Setup the Objective Coefficients (c)
            for (int j = 0; j < numDecVars; j++)
            {
                VariableNames[j] = "x" + (j + 1);
                c[j] = isMaximization ? model.ObjectiveCoefficients[j] : -model.ObjectiveCoefficients[j];
            }
            for (int j = 0; j < numSlackVars; j++)
            {
                VariableNames[numDecVars + j] = "s" + (j + 1);
                c[numDecVars + j] = 0; // Slack variables have 0 profit/cost
            }

            // 2. Setup the Constraint Matrix (A) and RHS (b)
            for (int i = 0; i < NumRows; i++)
            {
                for (int j = 0; j < numDecVars; j++)
                {
                    A[i, j] = model.Constraints[i].Coefficients[j];
                }
                A[i, numDecVars + i] = 1.0; // Add the slack variable to the matrix
                b[i] = model.Constraints[i].RHS;

                // Set initial Basic Variables to the slacks
                BV[i] = numDecVars + i;

                // Initial Inverse Basis is an Identity Matrix (1s diagonally, 0s elsewhere)
                B_inv[i, i] = 1.0;
            }
        }

        // Step 2: The Main Math Loop
        public void Solve()
        {
            int iteration = 0;
            IterationLog.AppendLine("\n==============================================================");
            IterationLog.AppendLine("         REVISED PRIMAL SIMPLEX INITIALIZED");
            IterationLog.AppendLine("==============================================================");

            while (true)
            {
                iteration++;
                string header = $"\n--- ITERATION {iteration} ---";
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(header);
                Console.ResetColor();
                IterationLog.AppendLine(header);

                // Calculate the current values of our Basic Variables: x_B = B^-1 * b
                double[] x_B = MultiplyMatrixVector(B_inv, b);
                LogBasicVariables(x_B);

                // --- PRICE OUT ---
                // We calculate the 'Simplex Multipliers' (Pi) to figure out opportunity costs.
                // Pi = c_B * B^-1
                double[] c_B = GetCb();
                double[] pi = MultiplyVectorMatrix(c_B, B_inv);

                IterationLog.AppendLine("\n[PRICE OUT PHASE]");
                IterationLog.AppendLine($"Simplex Multipliers (Pi): [{FormatArray(pi)}]");

                // Find the entering variable by calculating Reduced Costs: (Pi * A_j) - c_j
                int enteringCol = -1;
                double minReducedCost = -1e-7;

                for (int j = 0; j < NumCols; j++)
                {
                    // Skip if the variable is already basic
                    if (Array.IndexOf(BV, j) != -1) continue;

                    double[] A_j = GetColumn(A, j);
                    double zj = DotProduct(pi, A_j);
                    double reducedCost = zj - c[j];

                    if (reducedCost < minReducedCost)
                    {
                        minReducedCost = reducedCost;
                        enteringCol = j;
                    }
                }

                if (enteringCol == -1)
                {
                    string optMsg = "\n[SYSTEM] Optimality criterion satisfied. All reduced costs are >= 0.";
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(optMsg);
                    Console.ResetColor();
                    IterationLog.AppendLine(optMsg);
                    break;
                }

                string enterMsg = $"Entering Variable: {VariableNames[enteringCol]} (Reduced Cost: {minReducedCost:F3})";
                Console.WriteLine($"  {enterMsg}");
                IterationLog.AppendLine(enterMsg);

                // --- PRODUCT FORM ---
                // Calculate the specific entering column inside our current basis: d = B^-1 * A_j
                IterationLog.AppendLine("\n[PRODUCT FORM PHASE]");
                double[] entering_A_j = GetColumn(A, enteringCol);
                double[] d = MultiplyMatrixVector(B_inv, entering_A_j);
                IterationLog.AppendLine($"Computed Column (d = B^-1 * A_j): [{FormatArray(d)}]");

                // Ratio Test to find the leaving variable
                int leavingRow = -1;
                double minRatio = double.MaxValue;

                for (int i = 0; i < NumRows; i++)
                {
                    if (d[i] > 1e-7) // Only divide by positive numbers
                    {
                        double ratio = x_B[i] / d[i];
                        if (ratio < minRatio)
                        {
                            minRatio = ratio;
                            leavingRow = i;
                        }
                    }
                }

                if (leavingRow == -1)
                {
                    throw new InvalidOperationException("The model is UNBOUNDED. The computed entering column has no positive values.");
                }

                string leaveMsg = $"Leaving Variable: {VariableNames[BV[leavingRow]]} (Row {leavingRow + 1}, Min Ratio: {minRatio:F3})";
                Console.WriteLine($"  {leaveMsg}");
                IterationLog.AppendLine(leaveMsg);

                // Create the Eta Matrix (E) to update our Inverse Basis Matrix
                double[,] E = CreateIdentityMatrix(NumRows);
                for (int i = 0; i < NumRows; i++)
                {
                    if (i == leavingRow)
                        E[i, leavingRow] = 1.0 / d[leavingRow];
                    else
                        E[i, leavingRow] = -d[i] / d[leavingRow];
                }

                // New B^-1 = E * Old B^-1
                B_inv = MultiplyMatrixMatrix(E, B_inv);

                // Swap the variable out
                BV[leavingRow] = enteringCol;
            }
        }

        // Helper: Builds the standard SimplexResult care package so Member 3 & 4 don't know the difference.
        public SimplexResult GetResult()
        {
            SimplexResult result = new SimplexResult();

            // Reconstruct the 2D array matrix that standard simplex uses
            result.FinalTableau = new double[NumRows + 1, NumCols + 1];
            result.ColumnHeaders = new string[NumCols + 1];
            result.RowHeaders = new string[NumRows + 1];

            result.RowHeaders[0] = "Z";
            for (int j = 0; j < NumCols; j++) result.ColumnHeaders[j] = VariableNames[j];
            result.ColumnHeaders[NumCols] = "RHS";

            double[] c_B = GetCb();
            double[] pi = MultiplyVectorMatrix(c_B, B_inv);
            double[] b_bar = MultiplyMatrixVector(B_inv, b);

            // Rebuild Z-Row
            for (int j = 0; j < NumCols; j++)
            {
                double zj = DotProduct(pi, GetColumn(A, j));
                result.FinalTableau[0, j] = zj - c[j];
            }
            result.FinalTableau[0, NumCols] = DotProduct(pi, b); // Optimal Z value
            result.OptimalZ = result.FinalTableau[0, NumCols];

            // Rebuild Constraint Rows
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

            // Make sure non-basic variables are set to 0.0
            for (int j = 0; j < NumCols; j++)
            {
                if (Array.IndexOf(BV, j) == -1) result.VariableValues[VariableNames[j]] = 0.0;
            }

            return result;
        }

        // --- MATH HELPERS ---
        private void LogBasicVariables(double[] x_B)
        {
            IterationLog.Append("Current Basic Variables: ");
            for (int i = 0; i < NumRows; i++) IterationLog.Append($"{VariableNames[BV[i]]} = {x_B[i]:F3} | ");
            IterationLog.AppendLine();
        }

        private double[] GetCb()
        {
            double[] cb = new double[NumRows];
            for (int i = 0; i < NumRows; i++) cb[i] = c[BV[i]];
            return cb;
        }

        private double[] GetColumn(double[,] matrix, int colIndex)
        {
            double[] col = new double[NumRows];
            for (int i = 0; i < NumRows; i++) col[i] = matrix[i, colIndex];
            return col;
        }

        private double DotProduct(double[] v1, double[] v2)
        {
            double sum = 0;
            for (int i = 0; i < v1.Length; i++) sum += v1[i] * v2[i];
            return sum;
        }

        private double[] MultiplyVectorMatrix(double[] v, double[,] m)
        {
            double[] result = new double[m.GetLength(1)];
            for (int j = 0; j < m.GetLength(1); j++)
                for (int i = 0; i < v.Length; i++)
                    result[j] += v[i] * m[i, j];
            return result;
        }

        private double[] MultiplyMatrixVector(double[,] m, double[] v)
        {
            double[] result = new double[m.GetLength(0)];
            for (int i = 0; i < m.GetLength(0); i++)
                for (int j = 0; j < v.Length; j++)
                    result[i] += m[i, j] * v[j];
            return result;
        }

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

        private double[,] CreateIdentityMatrix(int size)
        {
            double[,] identity = new double[size, size];
            for (int i = 0; i < size; i++) identity[i, i] = 1.0;
            return identity;
        }

        private string FormatArray(double[] array)
        {
            string[] formatted = new string[array.Length];
            for (int i = 0; i < array.Length; i++) formatted[i] = array[i].ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            return string.Join(", ", formatted);
        }
    }
}