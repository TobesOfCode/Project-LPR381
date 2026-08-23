using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LPR381.Models;

namespace Project_LPR381.PostOptimality
{
    public class Duality
    {
        private LinearModel primalModel;

        public Duality(LinearModel primalModel)
        {
            this.primalModel = primalModel;
        }

        public LinearModel CreateDual()
        {
            var dual = new LinearModel();

            int numDualVars = primalModel.Constraints.Count;
            int numDualConstraints = primalModel.ObjectiveCoefficients.Count;

            dual.ObjectiveCoefficients = new List<double>();
            for (int i = 0; i < numDualVars; i++)
            {
                dual.ObjectiveCoefficients.Add(primalModel.Constraints[i].RHS);
            }

            dual.OptimizationType = primalModel.OptimizationType == "max" ? "min" : "max";

            dual.Constraints = new List<Constraint>();

            for (int j = 0; j < numDualConstraints; j++)
            {
                var dualConstraint = new Constraint();
                dualConstraint.Coefficients = new List<double>();

                for (int i = 0; i < numDualVars; i++)
                {
                    if (j < primalModel.Constraints[i].Coefficients.Count)
                    {
                        dualConstraint.Coefficients.Add(primalModel.Constraints[i].Coefficients[j]);
                    }
                    else
                    {
                        dualConstraint.Coefficients.Add(0);
                    }
                }

                dualConstraint.RHS = primalModel.ObjectiveCoefficients[j];

                string signRestriction = j < primalModel.SignRestrictions.Count ?
                    primalModel.SignRestrictions[j] : ">=0";

                dualConstraint.Relation = GetDualConstraintType(signRestriction);

                dual.Constraints.Add(dualConstraint);
            }

            dual.SignRestrictions = new List<string>();
            for (int i = 0; i < numDualVars; i++)
            {
                dual.SignRestrictions.Add(GetDualVariableSign(primalModel.Constraints[i].Relation));
            }

            return dual;
        }

        private string GetDualConstraintType(string signRestriction)
        {
            if (signRestriction == "free" || signRestriction == "unrestricted")
                return "=";

            if (primalModel.OptimizationType == "max")
            {
                return signRestriction == ">=0" ? ">=" : "<=";
            }
            else
            {
                return signRestriction == ">=0" ? "<=" : ">=";
            }
        }

        private string GetDualVariableSign(string primalRelation)
        {
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

        public void DisplayDual(LinearModel dual)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("                    DUAL PROBLEM                           ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            Console.WriteLine($"\n  Original: {primalModel.OptimizationType.ToUpper()}imize");
            Console.WriteLine($"  Dual:     {dual.OptimizationType.ToUpper()}imize");

            Console.WriteLine("\n  Objective Function:");
            Console.Write("    ");
            for (int i = 0; i < dual.ObjectiveCoefficients.Count; i++)
            {
                if (i > 0) Console.Write(" + ");
                Console.Write($"{dual.ObjectiveCoefficients[i]:F2} y{i + 1}");
            }
            Console.WriteLine();

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

        public void ShowDualityRelationship()
        {
            Console.Clear();
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

        public void ShowMenu()
        {
            bool keepShowing = true;

            while (keepShowing)
            {
                Console.Clear();
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
                        keepShowing = false;
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