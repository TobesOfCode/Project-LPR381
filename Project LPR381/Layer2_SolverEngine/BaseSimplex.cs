using System;
using LPR381.Models;

namespace LPR381.Layer2_SolverEngine
{
    // Purpose: Implements core mathematical simplex matrix computations, pivoting logic, and step-by-step console visualization.
    // Use case: Processes parsed linear models into 2D tabular arrays, safely ignoring bin/int tags during relaxed optimal tableau calculations.
    public class BaseSimplex
    {
        public double[,] Tableau { get; private set; }
        public int NumRows { get; private set; }
        public int NumCols { get; private set; }

        public string[] ColumnHeaders { get; private set; }
        public string[] RowHeaders { get; private set; }

        private bool isMaximization;

        // Purpose: Converts a 1D LinearModel object into a standard 2D Simplex tableau matrix.
        // Use case: Called prior to optimization to set up the initial canonical form matrix.
        public void InitializeTableau(LinearModel model)
        {
            isMaximization = model.OptimizationType == "max";
            NumRows = model.Constraints.Count + 1;

            int numDecisionVars = model.ObjectiveCoefficients.Count;
            int numSlackVars = model.Constraints.Count;
            NumCols = numDecisionVars + numSlackVars + 1;

            Tableau = new double[NumRows, NumCols];
            ColumnHeaders = new string[NumCols];
            RowHeaders = new string[NumRows];

            for (int j = 0; j < numDecisionVars; j++) ColumnHeaders[j] = "x" + (j + 1);
            for (int j = 0; j < numSlackVars; j++) ColumnHeaders[numDecisionVars + j] = "s" + (j + 1);
            ColumnHeaders[NumCols - 1] = "RHS";

            RowHeaders[0] = "Z";
            for (int i = 1; i < NumRows; i++) RowHeaders[i] = "s" + i;

            for (int j = 0; j < numDecisionVars; j++)
            {
                Tableau[0, j] = isMaximization ? -model.ObjectiveCoefficients[j] : model.ObjectiveCoefficients[j];
            }
            Tableau[0, NumCols - 1] = 0;

            for (int i = 0; i < model.Constraints.Count; i++)
            {
                int rowIndex = i + 1;
                for (int j = 0; j < numDecisionVars; j++)
                {
                    Tableau[rowIndex, j] = model.Constraints[i].Coefficients[j];
                }
                Tableau[rowIndex, numDecisionVars + i] = 1.0;
                Tableau[rowIndex, NumCols - 1] = model.Constraints[i].RHS;
            }
        }

        // Purpose: Executes the iterative simplex loop until an optimal or unbounded state is identified.
        // Use case: Run when standard Primal Simplex is selected from the UI menu.
        public void Solve()
        {
            int iterationCount = 0;
            PrintTableau(iterationCount);

            while (true)
            {
                int pivotCol = GetEnteringVariable();
                if (pivotCol == -1)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n  [SYSTEM] No more negative values in Z-Row. Optimal solution reached.");
                    Console.ResetColor();
                    break;
                }

                int pivotRow = GetLeavingVariable(pivotCol);
                if (pivotRow == -1) throw new Exception("Model is Unbounded.");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n  [PIVOT] {ColumnHeaders[pivotCol]} enters the basis | {RowHeaders[pivotRow]} leaves the basis");
                Console.ResetColor();

                PerformPivot(pivotRow, pivotCol);
                iterationCount++;
                PrintTableau(iterationCount);
            }
        }

        private int GetEnteringVariable()
        {
            int enteringCol = -1;
            double minValue = 0;

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

        private int GetLeavingVariable(int pivotCol)
        {
            int leavingRow = -1;
            double minRatio = double.MaxValue;

            for (int i = 1; i < NumRows; i++)
            {
                double coefficient = Tableau[i, pivotCol];
                if (coefficient > 0)
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

        private void PerformPivot(int pivotRow, int pivotCol)
        {
            RowHeaders[pivotRow] = ColumnHeaders[pivotCol];
            double pivotValue = Tableau[pivotRow, pivotCol];

            for (int j = 0; j < NumCols; j++) Tableau[pivotRow, j] /= pivotValue;

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

        // Purpose: Packages final calculated results and cloned matrix states into a secure SimplexResult DTO.
        // Use case: Called after optimization completes to safely pass optimal data to post-optimality or integer modules.
        public SimplexResult GetResult()
        {
            SimplexResult result = new SimplexResult();
            result.OptimalZ = Tableau[0, NumCols - 1];
            result.FinalTableau = (double[,])Tableau.Clone();
            result.RowHeaders = (string[])RowHeaders.Clone();
            result.ColumnHeaders = (string[])ColumnHeaders.Clone();

            for (int j = 0; j < NumCols - 1; j++)
            {
                string varName = ColumnHeaders[j];
                int rowIndex = Array.IndexOf(RowHeaders, varName);

                if (rowIndex != -1) result.VariableValues[varName] = Tableau[rowIndex, NumCols - 1];
                else result.VariableValues[varName] = 0;
            }

            return result;
        }

        // Purpose: Renders a formatted ASCII grid of the current tableau iteration into the console window.
        // Use case: Called after initialization and each pivot step to display rounded step-by-step matrix progress.
        public void PrintTableau(int iteration)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            if (iteration == 0) Console.WriteLine("\n  --- Initial Tableau (Canonical Form) ---");
            else Console.WriteLine($"\n  --- Iteration {iteration} ---");
            Console.ResetColor();

            Console.Write("  ╔══════");
            for (int j = 0; j < NumCols; j++) Console.Write("╦════════════");
            Console.WriteLine("╗");

            Console.Write("  ║  B.V ");
            for (int j = 0; j < NumCols; j++) Console.Write($"║ {ColumnHeaders[j],10} ");
            Console.WriteLine("║");

            Console.Write("  ╠══════");
            for (int j = 0; j < NumCols; j++) Console.Write("╬════════════");
            Console.WriteLine("╣");

            for (int i = 0; i < NumRows; i++)
            {
                Console.Write($"  ║ {RowHeaders[i],4} ");
                for (int j = 0; j < NumCols; j++) Console.Write($"║ {Tableau[i, j],10:F3} ");
                Console.WriteLine("║");

                if (i == 0)
                {
                    Console.Write("  ╠══════");
                    for (int j = 0; j < NumCols; j++) Console.Write("╬════════════");
                    Console.WriteLine("╣");
                }
            }

            Console.Write("  ╚══════");
            for (int j = 0; j < NumCols; j++) Console.Write("╩════════════");
            Console.WriteLine("╝");
        }
    }
}