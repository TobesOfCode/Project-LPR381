using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LPR381.Models;

namespace Project_LPR381.PostOptimality
{
    // -------------------------------------------------------------------------
    // CLASS: SensitivityAnalysis
    // Purpose: This module answers "What if?" questions after we've found the optimal answer.
    // It tells us how much we can mess with our limits (RHS) or our profits (Objective Coefficients) 
    // before our perfect strategy breaks and we have to recalculate everything from scratch.
    // -------------------------------------------------------------------------
    public class SensitivityAnalysis
    {
        // We keep safe copies of the final answer and the original problem to compare them.
        private SimplexResult optimalState;
        private LinearModel originalModel;

        public SensitivityAnalysis(SimplexResult result, LinearModel model)
        {
            this.optimalState = result;
            this.originalModel = model;
        }

        // -------------------------------------------------------------------------
        // METHOD: ShowMenu
        // Purpose: The dedicated user interface loop for Sensitivity Analysis.
        // -------------------------------------------------------------------------
        public void ShowMenu()
        {
            bool keepShowing = true;

            while (keepShowing)
            {
                // Clear the screen every time we loop back to this menu to keep the terminal neat!
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n  ╔════════════════════════════════════════════════════════╗");
                Console.WriteLine("  ║             SENSITIVITY ANALYSIS MENU                  ║");
                Console.WriteLine("  ╚════════════════════════════════════════════════════════╝");
                Console.ResetColor();

                Console.WriteLine($"\n  Optimal Z: {optimalState.OptimalZ:F4}");
                Console.WriteLine($"  Variables: {optimalState.VariableValues.Count}");
                Console.WriteLine($"  Constraints: {originalModel.Constraints.Count}\n");

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("  1. Shadow Prices");
                Console.WriteLine("  2. RHS Sensitivity");
                Console.WriteLine("  3. Objective Coefficient Sensitivity");
                Console.WriteLine("  4. Change RHS Value");
                Console.WriteLine("  5. Add New Activity");
                Console.WriteLine("  6. Full Sensitivity Report");
                Console.WriteLine("  0. Return");
                Console.WriteLine("  ──────────────────────────────────────────────────────────");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\n  Select an option [0-6]: ");
                Console.ResetColor();

                string input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        ShowShadowPrices();
                        break;
                    case "2":
                        ShowRHSSensitivity();
                        break;
                    case "3":
                        ShowObjectiveSensitivity();
                        break;
                    case "4":
                        ChangeRHS();
                        break;
                    case "5":
                        AddNewActivity();
                        break;
                    case "6":
                        ShowFullReport();
                        break;
                    case "0":
                        keepShowing = false; // Exits the loop and goes back
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\n  Invalid option. Please select 0-6.");
                        Console.ResetColor();
                        Console.ReadKey();
                        break;
                }
            }
        }

        // -------------------------------------------------------------------------
        // METHOD: ShowShadowPrices
        // Purpose: Shadow prices tell us exactly how much our total profit (Z) will 
        // improve if we increase a specific constraint's limit by exactly 1 unit.
        // -------------------------------------------------------------------------
        private void ShowShadowPrices()
        {
            Console.Clear(); // Clears the screen for a clean report
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("                    SHADOW PRICES                          ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            double[,] tableau = optimalState.FinalTableau;
            int numDecisionVars = originalModel.ObjectiveCoefficients.Count;
            int numConstraints = originalModel.Constraints.Count;

            Console.WriteLine("\n  Shadow Price = -Z_row[slack_column]\n");

            Console.WriteLine($"  {"Constraint",-20} {"Shadow Price",-15}");
            Console.WriteLine("  ──────────────────────────────────────────────────────────");

            // To find the shadow price, we just look at the Z-Row (top row) under the slack variables.
            for (int i = 0; i < numConstraints; i++)
            {
                int slackCol = numDecisionVars + i; // Jump past the x variables to find the slacks
                double shadowPrice = -tableau[0, slackCol]; // The math rule for shadow prices
                Console.WriteLine($"  Constraint {i + 1,-12} {shadowPrice,15:F4}");
            }

            Console.WriteLine("\n  Press any key to continue...");
            Console.ReadKey();
        }

        // -------------------------------------------------------------------------
        // METHOD: ShowRHSSensitivity
        // Purpose: Calculates the minimum and maximum boundaries for our Right-Hand Side 
        // limits (capacities). If we stay within this range, our current combination of variables stays optimal.
        // -------------------------------------------------------------------------
        private void ShowRHSSensitivity()
        {
            Console.Clear(); // Clears screen
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("                RHS SENSITIVITY ANALYSIS                   ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            double[,] tableau = optimalState.FinalTableau;
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);
            int rhsCol = cols - 1;

            Console.WriteLine("\n  Allowable range for RHS values before the optimal basis changes.\n");

            Console.WriteLine($"  {"Constraint",-15} {"Current RHS",-15} {"Allowable ↓",-15} {"Allowable ↑",-15}");
            Console.WriteLine("  ───────────────────────────────────────────────────────────────────");

            // Loop through each active constraint row to test its limits
            for (int i = 1; i < rows; i++)
            {
                string rowHeader = optimalState.RowHeaders[i];
                double currentRHS = tableau[i, rhsCol];

                double allowableDecrease;
                double allowableIncrease;

                // Let the math helper figure out the limits
                CalculateRHSRange(tableau, i, out allowableDecrease, out allowableIncrease);

                Console.WriteLine($"  {rowHeader,-15} {currentRHS,15:F4} {allowableDecrease,15:F4} {allowableIncrease,15:F4}");
            }

            Console.WriteLine("\n  Press any key to continue...");
            Console.ReadKey();
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: CalculateRHSRange
        // Purpose: The mathematical ratio test used to find the allowable RHS boundaries.
        // -------------------------------------------------------------------------
        private void CalculateRHSRange(double[,] tableau, int rowIndex, out double allowableDecrease, out double allowableIncrease)
        {
            int cols = tableau.GetLength(1);
            int rhsCol = cols - 1;

            allowableDecrease = double.MaxValue;
            allowableIncrease = double.MaxValue;

            for (int j = 0; j < cols - 1; j++)
            {
                double coeff = tableau[rowIndex, j];
                if (Math.Abs(coeff) < 1e-10) continue; // Ignore zeroes

                double rhsVal = tableau[rowIndex, rhsCol];

                // If the number is negative, it restricts how much we can INCREASE the RHS.
                if (coeff < 0)
                {
                    double ratio = -rhsVal / coeff;
                    if (ratio > 0 && ratio < allowableIncrease)
                        allowableIncrease = ratio;
                }
                // If the number is positive, it restricts how much we can DECREASE the RHS.
                else if (coeff > 0)
                {
                    double ratio = rhsVal / coeff;
                    if (ratio > 0 && ratio < allowableDecrease)
                        allowableDecrease = ratio;
                }
            }

            // If we didn't find a limit, we just display 1000 to represent "infinity"
            if (allowableDecrease == double.MaxValue) allowableDecrease = 1000.0;
            if (allowableIncrease == double.MaxValue) allowableIncrease = 1000.0;
        }

        // -------------------------------------------------------------------------
        // METHOD: ShowObjectiveSensitivity
        // Purpose: Calculates how much we can change our profit/cost margins (objective coefficients) 
        // before we have to change our entire strategy (the basis).
        // -------------------------------------------------------------------------
        private void ShowObjectiveSensitivity()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("           OBJECTIVE COEFFICIENT SENSITIVITY               ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            double[,] tableau = optimalState.FinalTableau;

            Console.WriteLine("\n  Allowable range for objective coefficients before the optimal solution changes.\n");

            Console.WriteLine($"  {"Variable",-12} {"Current",-12} {"Reduced Cost",-15} {"Basic?",-10} {"Allowable ↓",-15} {"Allowable ↑"}");
            Console.WriteLine("  ───────────────────────────────────────────────────────────────────────────────");

            // Look at every variable column in the grid
            for (int j = 0; j < optimalState.ColumnHeaders.Length - 1; j++)
            {
                string varName = optimalState.ColumnHeaders[j];
                double reducedCost = tableau[0, j];

                // Is this variable currently active in our solution?
                bool isBasic = optimalState.RowHeaders.Contains(varName);
                double currentCoeff = GetCoefficientForVariable(varName);

                double allowableDecrease;
                double allowableIncrease;

                // Let the math helper figure out the bounds
                CalculateObjectiveRange(j, isBasic, out allowableDecrease, out allowableIncrease);

                // Format "infinity" nicely instead of showing a massive system number
                string incText = allowableIncrease == double.MaxValue ? "∞" : allowableIncrease.ToString("F4");
                string decText = allowableDecrease == double.MaxValue ? "∞" : allowableDecrease.ToString("F4");

                Console.WriteLine($"  {varName,-12} {currentCoeff,12:F4} {reducedCost,15:F4} {(isBasic ? "Yes" : "No"),-10} {decText,15} {incText}");
            }

            Console.WriteLine("\n  Press any key to continue...");
            Console.ReadKey();
        }

        // Helper just retrieves the original profit/cost for a specific variable
        private double GetCoefficientForVariable(string variableName)
        {
            if (variableName.StartsWith("x"))
            {
                int.TryParse(variableName.Substring(1), out int num);
                int index = num - 1;
                if (index >= 0 && index < originalModel.ObjectiveCoefficients.Count)
                    return originalModel.ObjectiveCoefficients[index];
            }
            return 0;
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: CalculateObjectiveRange
        // Purpose: The mathematical ratio test used to find the allowable profit/cost boundaries.
        // -------------------------------------------------------------------------
        private void CalculateObjectiveRange(int colIndex, bool isBasic, out double allowableDecrease, out double allowableIncrease)
        {
            double[,] tableau = optimalState.FinalTableau;
            allowableDecrease = double.MaxValue;
            allowableIncrease = double.MaxValue;

            // If the variable IS NOT in our solution, changing its profit only affects its own reduced cost.
            if (!isBasic)
            {
                double reducedCost = tableau[0, colIndex];
                if (originalModel.OptimizationType == "max")
                {
                    allowableDecrease = -reducedCost;
                    if (allowableDecrease < 0) allowableDecrease = 0;
                }
                else
                {
                    allowableIncrease = -reducedCost;
                    if (allowableIncrease < 0) allowableIncrease = 0;
                }
            }
            // If the variable IS in our solution, changing its profit affects the ENTIRE Z-row!
            else
            {
                int rowIndex = Array.IndexOf(optimalState.RowHeaders, optimalState.ColumnHeaders[colIndex]);
                if (rowIndex != -1)
                {
                    for (int j = 0; j < tableau.GetLength(1) - 1; j++)
                    {
                        if (j == colIndex) continue;
                        double coeff = tableau[rowIndex, j];
                        double reducedCost = tableau[0, j];

                        if (Math.Abs(coeff) < 1e-10) continue; // Ignore zeroes

                        if (originalModel.OptimizationType == "max")
                        {
                            if (reducedCost < 0)
                            {
                                if (coeff > 0)
                                {
                                    double ratio = -reducedCost / coeff;
                                    if (ratio < allowableDecrease) allowableDecrease = ratio;
                                }
                                else
                                {
                                    double ratio = -reducedCost / coeff;
                                    if (ratio < allowableIncrease) allowableIncrease = ratio;
                                }
                            }
                        }
                    }
                }
            }

            if (allowableDecrease == double.MaxValue) allowableDecrease = double.PositiveInfinity;
            if (allowableIncrease == double.MaxValue) allowableIncrease = double.PositiveInfinity;
        }

        // -------------------------------------------------------------------------
        // METHOD: ChangeRHS
        // Purpose: Interactively lets the user tweak an RHS value (capacity limit) 
        // to see if the current math model survives or breaks.
        // -------------------------------------------------------------------------
        private void ChangeRHS()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("                 CHANGE RHS VALUE                          ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            double[,] tableau = optimalState.FinalTableau;
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);
            int rhsCol = cols - 1;

            Console.WriteLine("\n  Current RHS values:");
            for (int i = 1; i < rows; i++)
            {
                Console.WriteLine($"    {optimalState.RowHeaders[i],-10}: {tableau[i, rhsCol],15:F4}");
            }

            Console.Write("\n  Enter constraint number to change (1 - {0}): ", rows - 1);
            if (!int.TryParse(Console.ReadLine(), out int constraintIdx) || constraintIdx < 1 || constraintIdx >= rows)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Invalid constraint number!");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            int rowIndex = constraintIdx;
            Console.Write("  Enter new RHS value: ");
            if (!double.TryParse(Console.ReadLine(), out double newRHS))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Invalid value!");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            double oldRHS = tableau[rowIndex, rhsCol];
            Console.WriteLine($"\n  Old RHS: {oldRHS:F4}, New RHS: {newRHS:F4}");

            // Verify if the mathematical feasibility holds up (i.e. nothing becomes negative)
            bool feasible = true;
            for (int i = 1; i < rows; i++)
            {
                if (i == rowIndex) continue;
                double rhsVal = tableau[i, rhsCol] + newRHS * tableau[i, rhsCol] / tableau[rowIndex, rhsCol];
                if (rhsVal < -1e-7) // If RHS becomes negative, it's infeasible!
                {
                    feasible = false;
                    break;
                }
            }

            if (feasible)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n  Solution remains feasible with this RHS change.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n  This change makes the solution infeasible.");
                Console.WriteLine("  Re-optimization would be required.");
                Console.ResetColor();
            }

            Console.WriteLine("\n  Press any key to continue...");
            Console.ReadKey();
        }

        // -------------------------------------------------------------------------
        // METHOD: AddNewActivity
        // Purpose: Analyzes if adding a completely new variable (activity) is worth it 
        // mathematically. It checks if the "Reduced Cost" proves it would be profitable.
        // -------------------------------------------------------------------------
        private void AddNewActivity()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("               ADD NEW ACTIVITY                            ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            int numConstraints = originalModel.Constraints.Count;

            Console.WriteLine("\n  Enter coefficients for the new variable:");
            double[] newColumn = new double[numConstraints];

            for (int i = 0; i < numConstraints; i++)
            {
                Console.Write($"    Constraint {i + 1}: ");
                if (!double.TryParse(Console.ReadLine(), out newColumn[i]))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  Invalid coefficient!");
                    Console.ResetColor();
                    Console.ReadKey();
                    return;
                }
            }

            Console.Write("\n  Enter objective coefficient: ");
            if (!double.TryParse(Console.ReadLine(), out double objCoeff))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Invalid coefficient!");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            // In basic OR, we just check if the new reduced cost is favorable.
            double reducedCost = objCoeff;
            bool wouldImprove;
            if (originalModel.OptimizationType == "max")
                wouldImprove = reducedCost > 0.001; // Positive is good for maximize
            else
                wouldImprove = reducedCost < -0.001; // Negative is good for minimize

            if (wouldImprove)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n  Reduced cost = {reducedCost:F4}");
                Console.WriteLine("  Adding this variable would improve the objective!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n  Reduced cost = {reducedCost:F4}");
                Console.WriteLine("  Adding this variable would NOT improve the objective.");
                Console.ResetColor();
            }

            Console.WriteLine("\n  Press any key to continue...");
            Console.ReadKey();
        }

        // -------------------------------------------------------------------------
        // METHOD: ShowFullReport
        // Purpose: Combines all sensitivity metrics into a single screen view.
        // -------------------------------------------------------------------------
        private void ShowFullReport()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n  ╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║              FULL SENSITIVITY REPORT                   ║");
            Console.WriteLine("  ╚════════════════════════════════════════════════════════╝");
            Console.ResetColor();

            Console.WriteLine($"\n  OPTIMAL SOLUTION:");
            Console.WriteLine($"    Z* = {optimalState.OptimalZ:F4}");
            foreach (var varValue in optimalState.VariableValues)
            {
                Console.WriteLine($"    {varValue.Key} = {varValue.Value:F4}");
            }

            Console.WriteLine("\n  ──────────────────────────────────────────────────────────");

            // We use read key pauses here so the user isn't instantly flooded with numbers
            Console.WriteLine("  [Press any key to view Shadow Prices...]");
            Console.ReadKey();
            ShowShadowPrices(); // Console.Clear() fires inside here!

            Console.WriteLine("  [Press any key to view RHS Sensitivity...]");
            Console.ReadKey();
            ShowRHSSensitivity();

            Console.WriteLine("  [Press any key to view Objective Sensitivity...]");
            Console.ReadKey();
            ShowObjectiveSensitivity();

            Console.WriteLine("\n  Report Complete. Press any key to return...");
            Console.ReadKey();
        }
    }
}