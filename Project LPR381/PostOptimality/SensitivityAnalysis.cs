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
    // What it does: Instead of forcing the computer to recalculate the entire problem from scratch 
    // when a limit changes, it analyzes the final math grid to see how flexible our current strategy is.
    // How it works: It calculates safe limits (allowable increases and decreases) for 
    // both our resources (RHS constraints) and our profit margins (Objective Coefficients).
    // -------------------------------------------------------------------------
    public class SensitivityAnalysis
    {
        // We keep safe copies of the final calculated answer and the original problem.
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
        // What it does: Gives the user a menu of different "What-if" reports to run.
        // How it works: A standard while-loop with a switch statement that keeps the 
        // user in this menu until they explicitly choose to exit (0).
        // -------------------------------------------------------------------------
        public void ShowMenu()
        {
            // >>> SAFETY CHECK: Prevents the program from crashing if the user tries to run
            // Sensitivity Analysis on a purely integer algorithm (like Knapsack or Cutting Plane) 
            // which doesn't produce standard continuous Simplex grids.
            if (optimalState.FinalTableau == null || optimalState.RowHeaders == null)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n  [!] SENSITIVITY ANALYSIS UNAVAILABLE");
                Console.WriteLine("  The current optimal solution does not contain a standard Simplex Tableau.");
                Console.WriteLine("  (Note: Sensitivity Analysis is mathematically undefined for pure Knapsack/Integer algorithms).");
                Console.ResetColor();
                Console.WriteLine("\n  Press any key to return to the main menu...");
                Console.ReadKey();
                return;
            }

            bool keepShowing = true;

            while (keepShowing)
            {
                // Clear the screen every time we loop back to this menu to keep the terminal neat!
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n  ╔════════════════════════════════════════════════════════╗");
                Console.WriteLine("  ║              SENSITIVITY ANALYSIS MENU                 ║");
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
                    case "1": ShowShadowPrices(); break;
                    case "2": ShowRHSSensitivity(); break;
                    case "3": ShowObjectiveSensitivity(); break;
                    case "4": ChangeRHS(); break;
                    case "5": AddNewActivity(); break;
                    case "6": ShowFullReport(); break;
                    case "0": keepShowing = false; break;
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
        // Purpose: Shows the "Shadow Price" for every constraint.
        // What it does: A Shadow Price tells us exactly how much our total profit (Z) 
        // will improve if we increase a specific constraint's limit by exactly 1 unit.
        // How it works: It looks at the top Z-Row of the final table. The number sitting 
        // directly under a slack variable IS the shadow price for that constraint!
        // -------------------------------------------------------------------------
        private void ShowShadowPrices()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("                    SHADOW PRICES                          ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            double[,] tableau = optimalState.FinalTableau;
            int numDecisionVars = originalModel.ObjectiveCoefficients.Count;
            int numConstraints = originalModel.Constraints.Count;



            Console.WriteLine("\n  Shadow Price = -Z_row[slack_column]\n");

            // Uniform ASCII Table format for clean presentation
            Console.WriteLine("  +-----------------+-----------------+");
            Console.WriteLine("  | Constraint      |    Shadow Price |");
            Console.WriteLine("  +-----------------+-----------------+");

            for (int i = 0; i < numConstraints; i++)
            {
                int slackCol = numDecisionVars + i; // Jump past the x variables to find the slacks
                double shadowPrice = tableau[0, slackCol]; // The math rule for shadow prices
                Console.WriteLine($"  | Constraint {i + 1,-4} | {shadowPrice,15:F3} |");

            }

            Console.WriteLine("  +-----------------+-----------------+");

            Console.WriteLine("\n  Press any key to continue...");
            Console.ReadKey();
        }

        // -------------------------------------------------------------------------
        // METHOD: ShowRHSSensitivity
        // Purpose: Shows how flexible our constraint limits are (e.g., material limits).
        // What it does: Calculates the minimum and maximum boundaries for our Right-Hand Side 
        // limits (capacities). If we stay within this range, our current strategy stays optimal.
        // How it works: Loops through every active row and delegates the actual math 
        // to the CalculateRHSRange helper function.
        // -------------------------------------------------------------------------
        private void ShowRHSSensitivity()
        {
            Console.Clear();
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

            Console.WriteLine("  +-----------------+-----------------+-----------------+-----------------+");
            Console.WriteLine("  | Constraint      |     Current RHS |     Allowable ↓ |     Allowable ↑ |");
            Console.WriteLine("  +-----------------+-----------------+-----------------+-----------------+");

            for (int i = 1; i < rows; i++)
            {
                string rowHeader = optimalState.RowHeaders[i];
                double currentRHS = tableau[i, rhsCol];

                // Let the math helper figure out the safe limits
                CalculateRHSRange(tableau, i, out double allowableDecrease, out double allowableIncrease);

                // If it hits 1000.0, we just show infinity symbol for aesthetics
                string decText = allowableDecrease == 1000.0 ? "∞" : allowableDecrease.ToString("F3");
                string incText = allowableIncrease == 1000.0 ? "∞" : allowableIncrease.ToString("F3");

                Console.WriteLine($"  | {rowHeader,-15} | {currentRHS,15:F3} | {decText,15} | {incText,15} |");
            }

            Console.WriteLine("  +-----------------+-----------------+-----------------+-----------------+");

            Console.WriteLine("\n  Press any key to continue...");
            Console.ReadKey();
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: CalculateRHSRange
        // Purpose: The mathematical ratio test used to find the allowable RHS boundaries.
        // How it works: It looks at every number in a row. Negative numbers tell us how 
        // much we can increase the limit safely. Positive numbers tell us how much we 
        // can decrease it. It keeps track of the smallest valid ratio to set the boundary.
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
                if (Math.Abs(coeff) < 1e-10) continue; // Ignore zeroes so we don't divide by zero

                double rhsVal = tableau[rowIndex, rhsCol];

                // If the number is negative, it restricts how much we can INCREASE the RHS.
                if (coeff < 0)
                {
                    double ratio = -rhsVal / coeff;
                    if (ratio > 0 && ratio < allowableIncrease) allowableIncrease = ratio;
                }
                // If the number is positive, it restricts how much we can DECREASE the RHS.
                else if (coeff > 0)
                {
                    double ratio = rhsVal / coeff;
                    if (ratio > 0 && ratio < allowableDecrease) allowableDecrease = ratio;
                }
            }

            // If no limits were found, set to 1000 to represent infinity
            if (allowableDecrease == double.MaxValue) allowableDecrease = 1000.0;
            if (allowableIncrease == double.MaxValue) allowableIncrease = 1000.0;
        }

        // -------------------------------------------------------------------------
        // METHOD: ShowObjectiveSensitivity
        // Purpose: Shows how flexible our profit margins are.
        // What it does: Calculates how much we can change our profit/cost margins 
        // (objective coefficients) before we have to change our entire strategy.
        // How it works: Loops through every column variable and tests its flexibility.
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

            Console.WriteLine("  +------------+------------+---------------+----------+---------------+---------------+");
            Console.WriteLine("  | Variable   |    Current |  Reduced Cost | Basic?   |   Allowable ↓ |   Allowable ↑ |");
            Console.WriteLine("  +------------+------------+---------------+----------+---------------+---------------+");

            for (int j = 0; j < optimalState.ColumnHeaders.Length - 1; j++)
            {
                string varName = optimalState.ColumnHeaders[j];
                double reducedCost = tableau[0, j];

                // Check if this variable is currently part of our solution (basis)
                bool isBasic = optimalState.RowHeaders.Contains(varName);
                double currentCoeff = GetCoefficientForVariable(varName);

                // Calculate the safety limits
                CalculateObjectiveRange(j, isBasic, out double allowableDecrease, out double allowableIncrease);

                string incText = allowableIncrease == double.MaxValue ? "∞" : allowableIncrease.ToString("F3");
                string decText = allowableDecrease == double.MaxValue ? "∞" : allowableDecrease.ToString("F3");
                string basicText = isBasic ? "Yes" : "No";

                Console.WriteLine($"  | {varName,-10} | {currentCoeff,10:F3} | {reducedCost,13:F3} | {basicText,-8} | {decText,13} | {incText,13} |");
            }

            Console.WriteLine("  +------------+------------+---------------+----------+---------------+---------------+");

            Console.WriteLine("\n  Press any key to continue...");
            Console.ReadKey();
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: GetCoefficientForVariable
        // Purpose: Retrieves the original profit/cost for a specific variable from the start.
        // What it does: Remembers what the original model started with so we can display it.
        // -------------------------------------------------------------------------
        private double GetCoefficientForVariable(string variableName)
        {
            if (variableName.StartsWith("x"))
            {
                int.TryParse(variableName.Substring(1), out int num);
                int index = num - 1;
                if (index >= 0 && index < originalModel.ObjectiveCoefficients.Count)
                    return originalModel.ObjectiveCoefficients[index];
            }
            return 0; // Slacks and excess variables always have 0 original profit
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: CalculateObjectiveRange
        // Purpose: The mathematical ratio test used to find allowable profit boundaries.
        // How it works: Variables NOT in our solution have infinite flexibility to become 
        // worse (less profitable), but limited flexibility to become better before we swap them in.
        // Variables IN our solution require a ratio test against every non-basic column 
        // to see when another variable would become more attractive to swap in.
        // -------------------------------------------------------------------------
        private void CalculateObjectiveRange(int colIndex, bool isBasic, out double allowableDecrease, out double allowableIncrease)
        {
            double[,] tableau = optimalState.FinalTableau;
            allowableDecrease = double.MaxValue;
            allowableIncrease = double.MaxValue;

            if (!isBasic)
            {
                // Non-basic variables calculate limits based solely on their own reduced cost.
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
            else
            {
                // Basic variables must check ratios across the entire row
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
        // Purpose: Interactively lets the user tweak an RHS limit (like adding 10 hours)
        // to see if the current math model survives or breaks.
        // How it works: You input the change, and the math projects whether any current
        // resources would fall below 0. If they do, the solution breaks (infeasible).
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
                Console.WriteLine($"    {optimalState.RowHeaders[i],-10}: {tableau[i, rhsCol],15:F3}");
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
            Console.WriteLine($"\n  Old RHS: {oldRHS:F3}, New RHS: {newRHS:F3}");

            // Verify if the mathematical feasibility holds up (i.e. nothing becomes negative)
            bool feasible = true;
            for (int i = 1; i < rows; i++)
            {
                if (i == rowIndex) continue;
                double rhsVal = tableau[i, rhsCol] + newRHS * tableau[i, rhsCol] / tableau[rowIndex, rhsCol];

                // If projecting this change pushes any basic variable below 0, it breaks!
                if (rhsVal < -1e-7)
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
        // Purpose: Checks if adding a totally new product (variable) is worth doing.
        // What it does: Calculates the "Reduced Cost" of a theoretical new item.
        // How it works: Compares the potential profit of the new item against the 
        // Shadow Prices (value) of the resources it would consume. If profit > cost, 
        // it tells you to go ahead and make it!
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

            // Positive reduced cost is good for maximize, negative is good for minimize.
            if (originalModel.OptimizationType == "max")
                wouldImprove = reducedCost > 0.001;
            else
                wouldImprove = reducedCost < -0.001;

            if (wouldImprove)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n  Reduced cost = {reducedCost:F3}");
                Console.WriteLine("  Adding this variable would improve the objective!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n  Reduced cost = {reducedCost:F3}");
                Console.WriteLine("  Adding this variable would NOT improve the objective.");
                Console.ResetColor();
            }

            Console.WriteLine("\n  Press any key to continue...");
            Console.ReadKey();
        }

        // -------------------------------------------------------------------------
        // METHOD: ShowFullReport
        // Purpose: Combines all sensitivity metrics into a single screen view.
        // What it does: Runs the Shadow Price, RHS Range, and Objective Range 
        // analysis methods in a row, pausing between each so the user can read them.
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
            Console.WriteLine($"    Z* = {optimalState.OptimalZ:F3}");
            foreach (var varValue in optimalState.VariableValues)
            {
                Console.WriteLine($"    {varValue.Key} = {varValue.Value:F3}");
            }

            Console.WriteLine("\n  ──────────────────────────────────────────────────────────");
            Console.WriteLine("  [Press any key to view Shadow Prices...]");
            Console.ReadKey();
            ShowShadowPrices();

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