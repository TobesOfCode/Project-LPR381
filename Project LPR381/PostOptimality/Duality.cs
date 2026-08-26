using System;
using System.Collections.Generic;
using LPR381.Models;

namespace Project_LPR381.PostOptimality
{
    // -------------------------------------------------------------------------
    // CLASS: Duality
    // Purpose: Duality is a massive part of Operations Research. Every "Primal" 
    // problem (like maximizing profit) has a shadow "Dual" problem (like minimizing the cost of resources). 
    // This class mathematically flips the original problem on its head so we can analyze it from that second perspective.
    // -------------------------------------------------------------------------
    public class Duality
    {
        // We store the original (Primal) problem here so we can read its structure.
        private LinearModel primalModel;

        public Duality(LinearModel primalModel)
        {
            this.primalModel = primalModel;
        }

        // -------------------------------------------------------------------------
        // METHOD: CreateDual
        // Purpose: This is where the magic happens. It takes the original math model 
        // and literally transposes it (turns rows into columns, and columns into rows).
        // -------------------------------------------------------------------------
        public LinearModel CreateDual()
        {
            var dual = new LinearModel();

            // In Duality, the number of constraints becomes the number of variables, 
            // and the number of variables becomes the number of constraints!
            int numDualVars = primalModel.Constraints.Count;
            int numDualConstraints = primalModel.ObjectiveCoefficients.Count;

            dual.ObjectiveCoefficients = new List<double>();

            // DUAL RULE 1: The Right-Hand Side (RHS) limits of the original problem 
            // become the new Objective Function coefficients (the Z-Row).
            for (int i = 0; i < numDualVars; i++)
            {
                dual.ObjectiveCoefficients.Add(primalModel.Constraints[i].RHS);
            }

            // DUAL RULE 2: If we were Maximizing, we are now Minimizing (and vice versa).
            dual.OptimizationType = primalModel.OptimizationType == "max" ? "min" : "max";

            dual.Constraints = new List<Constraint>();

            // Now we build the new constraints by reading the original ones vertically instead of horizontally.
            for (int j = 0; j < numDualConstraints; j++)
            {
                var dualConstraint = new Constraint();
                dualConstraint.Coefficients = new List<double>();

                // Transposing the grid (reading columns as rows)
                for (int i = 0; i < numDualVars; i++)
                {
                    if (j < primalModel.Constraints[i].Coefficients.Count)
                    {
                        dualConstraint.Coefficients.Add(primalModel.Constraints[i].Coefficients[j]);
                    }
                    else
                    {
                        dualConstraint.Coefficients.Add(0); // Safety fill if the matrix is uneven
                    }
                }

                // DUAL RULE 3: The old Objective Function coefficients (Z-Row) become the new RHS limits.
                dualConstraint.RHS = primalModel.ObjectiveCoefficients[j];

                // Determine if this new constraint should be <=, >=, or = based on the original variable's rules.
                string signRestriction = j < primalModel.SignRestrictions.Count ?
                    primalModel.SignRestrictions[j] : ">=0";

                dualConstraint.Relation = GetDualConstraintType(signRestriction);

                dual.Constraints.Add(dualConstraint);
            }

            // Finally, figure out the sign restrictions (like >= 0) for our new dual variables.
            dual.SignRestrictions = new List<string>();
            for (int i = 0; i < numDualVars; i++)
            {
                dual.SignRestrictions.Add(GetDualVariableSign(primalModel.Constraints[i].Relation));
            }

            return dual;
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: GetDualConstraintType
        // Purpose: Maps the original variable's sign restriction to the new constraint's operator.
        // -------------------------------------------------------------------------
        private string GetDualConstraintType(string signRestriction)
        {
            // If the original variable could be anything (free), the new constraint must be exactly equal (=).
            if (signRestriction == "free" || signRestriction == "unrestricted")
                return "=";

            // Otherwise, we flip the signs based on standard Operations Research tables.
            if (primalModel.OptimizationType == "max")
            {
                return signRestriction == ">=0" ? ">=" : "<=";
            }
            else
            {
                return signRestriction == ">=0" ? "<=" : ">=";
            }
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: GetDualVariableSign
        // Purpose: Maps the original constraint's operator to the new variable's sign restriction.
        // -------------------------------------------------------------------------
        private string GetDualVariableSign(string primalRelation)
        {
            // If the original constraint was an exact equal (=), the new variable can be anything (free).
            if (primalRelation == "=")
                return "free";

            if (primalModel.OptimizationType == "max")
            {
                return primalRelation == "<=" ? ">=0" : "<=0";
            }
            else
            {
                return primalRelation == ">=" ? ">=0" : "<=0";
            }
        }

        // -------------------------------------------------------------------------
        // METHOD: DisplayDual
        // Purpose: Prints out the newly generated Dual problem nicely to the console.
        // -------------------------------------------------------------------------
        public void DisplayDual(LinearModel dual)
        {
            Console.Clear(); // Clears the screen for a clean, fresh view of the math model.
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("                    DUAL PROBLEM                           ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            Console.WriteLine($"\n  Original: {primalModel.OptimizationType.ToUpper()}imize");
            Console.WriteLine($"  Dual:     {dual.OptimizationType.ToUpper()}imize");

            // Print the new Objective Function (Z-Row)
            Console.WriteLine("\n  Objective Function:");
            Console.Write("    ");
            for (int i = 0; i < dual.ObjectiveCoefficients.Count; i++)
            {
                if (i > 0) Console.Write(" + ");
                Console.Write($"{dual.ObjectiveCoefficients[i]:F2} y{i + 1}"); // We use 'y' for dual variables!
            }
            Console.WriteLine();

            // Print the new Constraints
            Console.WriteLine("\n  Subject to:");
            for (int i = 0; i < dual.Constraints.Count; i++)
            {
                var constraint = dual.Constraints[i];
                Console.Write("    ");
                for (int j = 0; j < constraint.Coefficients.Count; j++)
                {
                    if (j > 0) Console.Write(" + ");
                    Console.Write($"{constraint.Coefficients[j]:F2} y{j + 1}");
                }
                Console.WriteLine($" {constraint.Relation} {constraint.RHS:F2}");
            }

            // Print the new Sign Restrictions
            Console.WriteLine("\n  With:");
            for (int i = 0; i < dual.SignRestrictions.Count; i++)
            {
                string displaySign = dual.SignRestrictions[i] == "free" ? "free" :
                                    dual.SignRestrictions[i] == ">=0" ? ">= 0" :
                                    dual.SignRestrictions[i] == "<=0" ? "<= 0" :
                                    dual.SignRestrictions[i];
                Console.WriteLine($"    y{i + 1} {displaySign}");
            }

            Console.WriteLine("\n  Press any key to continue...");
            Console.ReadKey();
        }

        // -------------------------------------------------------------------------
        // METHOD: ShowDualityRelationship
        // Purpose: A helpful cheat-sheet for the user to understand how the rules flip.
        // -------------------------------------------------------------------------
        public void ShowDualityRelationship()
        {
            Console.Clear(); // Ensure the screen is cleared when accessing the cheat-sheet
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("              PRIMAL DUAL RELATIONSHIP                     ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            Console.WriteLine("\n  Primal vs Dual Mapping:");
            Console.WriteLine("  ──────────────────────────────────────────────────────────");
            Console.WriteLine($"  {"Primal",-25} -> {"Dual",-25}");
            Console.WriteLine("  ──────────────────────────────────────────────────────────");
            Console.WriteLine($"  {"Variables",-25} -> {"Constraints"}");
            Console.WriteLine($"  {"Constraints",-25} -> {"Variables"}");
            Console.WriteLine($"  {"Objective Coefficients",-25} -> {"RHS Values"}");
            Console.WriteLine($"  {"RHS Values",-25} -> {"Objective Coefficients"}");
            Console.WriteLine($"  {"Maximize",-25} -> {"Minimize"}");
            Console.WriteLine($"  {"Minimize",-25} -> {"Maximize"}");
            Console.WriteLine($"  {"<= Constraints",-25} -> {">= Variables (for max)"}");
            Console.WriteLine($"  {">= Constraints",-25} -> {"<= Variables (for max)"}");
            Console.WriteLine($"  {"= Constraints",-25} -> {"Free Variables"}");

            Console.WriteLine("\n  Press any key to continue...");
            Console.ReadKey();
        }

        // -------------------------------------------------------------------------
        // METHOD: ShowMenu
        // Purpose: The dedicated user interface loop for the Duality module.
        // -------------------------------------------------------------------------
        public void ShowMenu()
        {
            bool keepShowing = true;

            while (keepShowing)
            {
                Console.Clear(); // Clears the screen every time we loop back to this menu
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n  ╔════════════════════════════════════════════════════════╗");
                Console.WriteLine("  ║                    DUALITY MENU                       ║");
                Console.WriteLine("  ╚════════════════════════════════════════════════════════╝");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("\n  1. Create and Display Dual Problem");
                Console.WriteLine("  2. Show Primal Dual Relationship");
                Console.WriteLine("  0. Return");
                Console.WriteLine("  ──────────────────────────────────────────────────────────");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\n  Select an option [0-2]: ");
                Console.ResetColor();

                string input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        var dual = CreateDual();
                        DisplayDual(dual);
                        break;
                    case "2":
                        ShowDualityRelationship();
                        break;
                    case "0":
                        keepShowing = false; // Exits the loop and returns to the previous menu
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\n  Invalid option.");
                        Console.ResetColor();
                        Console.ReadKey();
                        break;
                }
            }
        }
    }
}