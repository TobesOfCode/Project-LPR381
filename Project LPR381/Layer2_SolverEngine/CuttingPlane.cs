using System;

namespace Project_LPR381.Layer2_SolverEngine
{
    internal class CuttingPlane
    {
        /// <summary>
        /// Member 3: Finds fractional values, derives Gomory cut, expands tableau, and solves via Dual Simplex.
        /// </summary>
        public static double[,] ApplyGomoryCutAndSolve(double[,] finalTableau, out bool cutApplied)
        {
            int rows = finalTableau.GetLength(0);
            int cols = finalTableau.GetLength(1);

            int targetRow = -1;
            double largestFraction = 0.0;

            // 1. Locate fractional RHS value (last column)
            for (int r = 1; r < rows; r++)
            {
                double rhsValue = finalTableau[r, cols - 1];
                double fraction = rhsValue - Math.Floor(rhsValue);

                if (fraction > 0.0001 && fraction < 0.9999)
                {
                    if (fraction > largestFraction)
                    {
                        largestFraction = fraction;
                        targetRow = r;
                    }
                }
            }

            if (targetRow == -1)
            {
                cutApplied = false;
                return finalTableau;
            }

            // 2. Expand array: +1 row for cut, +1 column for slack variable
            double[,] expandedTableau = new double[rows + 1, cols + 1];

            // 3. Copy existing tableau
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols - 1; c++)
                {
                    expandedTableau[r, c] = finalTableau[r, c];
                }
                expandedTableau[r, cols] = finalTableau[r, cols - 1];
            }

            // 4. Construct Gomory Cut row
            int newRow = rows;
            int newSlackCol = cols - 1;

            for (int c = 0; c < cols - 1; c++)
            {
                double coef = finalTableau[targetRow, c];
                double fracCoef = coef - Math.Floor(coef);
                expandedTableau[newRow, c] = -fracCoef;
            }

            expandedTableau[newRow, newSlackCol] = 1.0;
            expandedTableau[newRow, cols] = -largestFraction;

            // 5. Run Dual Simplex on expanded matrix
            RunDualSimplex(expandedTableau);

            cutApplied = true;
            return expandedTableau;
        }

        private static void RunDualSimplex(double[,] tableau)
        {
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);

            while (true)
            {
                int pivotRow = -1;
                double minRhs = 0;

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

                if (pivotCol == -1) throw new InvalidOperationException("Infeasible IP model.");

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
            }
        }
    }
}