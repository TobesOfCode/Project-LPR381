using System;
using System.IO;
using System.Collections.Generic;
using LPR381.Models;

namespace LPR381.Layer1_IO
{
    // Layer 1: The Parser. 
    // Its entire job is to read the messy text file line-by-line and translate it into our clean C# LinearModel object.
    public class Parser
    {
        public static LinearModel ScanFile(string filePath)
        {
            // First, make sure the file actually exists before we try to read it.
            if (!File.Exists(filePath)) throw new FileNotFoundException($"The file at {filePath} could not be found.");

            // Create an empty blueprint to hold our data.
            LinearModel model = new LinearModel();
            List<string> rawLines = new List<string>();

            // Open the file and read it line-by-line. We ignore empty lines and clean up extra spaces.
            using (StreamReader sr = new StreamReader(filePath))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line)) rawLines.Add(System.Text.RegularExpressions.Regex.Replace(line.Trim(), @"\s+", " "));
                }
            }

            // We need at least 3 lines: an objective, one constraint, and the sign restrictions.
            if (rawLines.Count < 3) throw new FormatException("Invalid file format. A minimum of 3 rows (Objective, Constraint, Sign Restrictions) is required.");

            // 1. Read the very first line (The Objective Function)
            string[] objTokens = rawLines[0].Split(' ');
            model.OptimizationType = objTokens[0].ToLower();
            if (model.OptimizationType != "max" && model.OptimizationType != "min")
            {
                throw new FormatException($"Invalid optimization type '{objTokens[0]}'. Must be 'max' or 'min'.");
            }
            // Send the rest of the numbers on line 1 to our helper method to convert them from text to doubles.
            model.ObjectiveCoefficients = ExtractCoefficients(objTokens, 1);

            // 2. Loop through all the middle lines (The Constraints)
            for (int i = 1; i < rawLines.Count - 1; i++)
            {
                string[] conTokens = rawLines[i].Split(' ');
                Constraint constraint = new Constraint();

                // Find where the <=, >=, or = sign is hiding in the line
                int relationIndex = Array.FindIndex(conTokens, t => t == "<=" || t == ">=" || t == "=");
                if (relationIndex == -1) throw new FormatException($"Missing relation operator (<=, >=, =) in constraint row {i}");

                constraint.Relation = conTokens[relationIndex];

                // The number right after the operator is our RHS limit
                constraint.RHS = double.Parse(conTokens[relationIndex + 1], System.Globalization.CultureInfo.InvariantCulture);

                // Everything before the operator is a coefficient. Pull them out and convert them.
                string[] coeffTokens = new string[relationIndex];
                Array.Copy(conTokens, coeffTokens, relationIndex);
                constraint.Coefficients = ExtractCoefficients(coeffTokens, 0);

                if (constraint.Coefficients.Count != model.ObjectiveCoefficients.Count)
                {
                    throw new FormatException($"Constraint row {i} has {constraint.Coefficients.Count} coefficients, expected {model.ObjectiveCoefficients.Count}.");
                }

                model.Constraints.Add(constraint);
            }

            // 3. Read the very last line (The Sign Restrictions)
            string[] signTokens = rawLines[rawLines.Count - 1].Split(' ');
            foreach (var token in signTokens)
            {
                model.SignRestrictions.Add(token.ToLower());
            }

            return model;
        }

        // Helper method: Takes text like "+ 5 - 2" and turns it into actual numbers (5.0, -2.0)
        private static List<double> ExtractCoefficients(string[] tokens, int startIndex)
        {
            List<double> coefficients = new List<double>();
            for (int i = startIndex; i < tokens.Length; i++)
            {
                if (tokens[i] == "+" || tokens[i] == "-")
                {
                    if (i + 1 >= tokens.Length) throw new FormatException("Trailing unary operator found without matching coefficient value.");
                    double value = double.Parse(tokens[i + 1], System.Globalization.CultureInfo.InvariantCulture);
                    if (tokens[i] == "-") value = -value;
                    coefficients.Add(value);
                    i++;
                }
                else
                {
                    coefficients.Add(double.Parse(tokens[i], System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            return coefficients;
        }

        // Helper method: Prints a clean summary of what we just parsed so the user can verify it's correct.
        public static void PrintIngestedModel(LinearModel model)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  +------------------------------------------------------------+");
            Console.WriteLine("  |                  PARSER INGESTION REPORT                   |");
            Console.WriteLine("  +------------------------------------------------------------+");

            Console.Write("    MODEL TYPE      : ");
            Console.ForegroundColor = model.OptimizationType == "max" ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.Write($"{model.OptimizationType.ToUpper()}\n");
            Console.ForegroundColor = ConsoleColor.Cyan;

            Console.WriteLine($"    OBJ COEFFICIENTS: [ {string.Join(", ", model.ObjectiveCoefficients)} ]");
            Console.WriteLine($"    TOTAL CONSTR.   : {model.Constraints.Count}");
            Console.WriteLine("  +------------------------------------------------------------+");
            Console.WriteLine("    CONSTRAINTS BREAKDOWN:");

            int index = 1;
            foreach (var con in model.Constraints)
            {
                string constraintStr = $"[{string.Join(", ", con.Coefficients)}] {con.Relation} {con.RHS}";
                Console.WriteLine($"      [{index++}] {constraintStr}");
            }

            Console.WriteLine("  +------------------------------------------------------------+");
            Console.WriteLine($"    SIGN RESTRICT.  : [ {string.Join(", ", model.SignRestrictions)} ]");
            Console.WriteLine("  +------------------------------------------------------------+\n");
            Console.ResetColor();
        }
    }
}