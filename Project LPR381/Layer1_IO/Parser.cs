using System;
using System.IO;
using System.Collections.Generic;
using LPR381.Models;

namespace LPR381.Layer1_IO
{
    // -------------------------------------------------------------------------
    // CLASS: Parser (Layer 1)
    // Purpose: This is the translator. Its entire job is to read the messy, 
    // human-written text file line-by-line and carefully translate it into our 
    // clean C# LinearModel object so the rest of the program can do the math.
    // -------------------------------------------------------------------------
    public class Parser
    {
        // -------------------------------------------------------------------------
        // METHOD: ScanFile
        // Purpose: Reads the text file and slices it into logical pieces (Objective, 
        // Constraints, and Sign Restrictions).
        // -------------------------------------------------------------------------
        public static LinearModel ScanFile(string filePath)
        {
            // First, a quick safety check to make sure the file actually exists on the hard drive.
            if (!File.Exists(filePath)) throw new FileNotFoundException($"The file at {filePath} could not be found.");

            // Create an empty blueprint to hold our translated data.
            LinearModel model = new LinearModel();
            List<string> rawLines = new List<string>();

            // Open the file and read it line-by-line. 
            // We ignore empty lines and use a Regex tool to squash multiple spaces into a single space, 
            // making it much easier to chop up later.
            using (StreamReader sr = new StreamReader(filePath))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line)) rawLines.Add(System.Text.RegularExpressions.Regex.Replace(line.Trim(), @"\s+", " "));
                }
            }

            // We mathematically need at least 3 lines: an objective, at least one constraint, and the sign restrictions.
            if (rawLines.Count < 3) throw new FormatException("Invalid file format. A minimum of 3 rows (Objective, Constraint, Sign Restrictions) is required.");

            // 1. Parse the very first line (The Objective Function)
            // We split the line by spaces. The first word should be "max" or "min".
            string[] objTokens = rawLines[0].Split(' ');
            model.OptimizationType = objTokens[0].ToLower();
            if (model.OptimizationType != "max" && model.OptimizationType != "min")
            {
                throw new FormatException($"Invalid optimization type '{objTokens[0]}'. Must be 'max' or 'min'.");
            }

            // Send the rest of the numbers on line 1 to our helper method to convert them from text to C# doubles.
            model.ObjectiveCoefficients = ExtractCoefficients(objTokens, 1);

            // 2. Loop through all the middle lines (The Constraints)
            for (int i = 1; i < rawLines.Count - 1; i++)
            {
                string[] conTokens = rawLines[i].Split(' ');
                Constraint constraint = new Constraint();

                // Find where the <=, >=, or = sign is hiding in the line.
                int relationIndex = Array.FindIndex(conTokens, t => t == "<=" || t == ">=" || t == "=");
                if (relationIndex == -1) throw new FormatException($"Missing relation operator (<=, >=, =) in constraint row {i}");

                constraint.Relation = conTokens[relationIndex];

                // The number directly after the operator is our RHS capacity limit.
                constraint.RHS = double.Parse(conTokens[relationIndex + 1], System.Globalization.CultureInfo.InvariantCulture);

                // Everything before the operator is a coefficient. Pull them out and convert them!
                string[] coeffTokens = new string[relationIndex];
                Array.Copy(conTokens, coeffTokens, relationIndex);
                constraint.Coefficients = ExtractCoefficients(coeffTokens, 0);

                // A quick safety check: we must have the exact same number of variables in our constraint as our objective function.
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

        // -------------------------------------------------------------------------
        // HELPER METHOD: ExtractCoefficients
        // Purpose: Takes messy text (like "+ 5 - 2") and turns it into actual math numbers.
        // How it works: It looks for '+' or '-' signs. If it finds one, it pairs it 
        // with the number right next to it so we capture negative values correctly.
        // -------------------------------------------------------------------------
        private static List<double> ExtractCoefficients(string[] tokens, int startIndex)
        {
            List<double> coefficients = new List<double>();
            for (int i = startIndex; i < tokens.Length; i++)
            {
                if (tokens[i] == "+" || tokens[i] == "-")
                {
                    if (i + 1 >= tokens.Length) throw new FormatException("Trailing unary operator found without matching coefficient value.");

                    // Parse the number, forcing InvariantCulture so standard dots are used instead of commas for decimals.
                    double value = double.Parse(tokens[i + 1], System.Globalization.CultureInfo.InvariantCulture);
                    if (tokens[i] == "-") value = -value; // Flip it to negative if we saw a minus sign!
                    coefficients.Add(value);
                    i++; // Skip the next token since we just used it
                }
                else
                {
                    coefficients.Add(double.Parse(tokens[i], System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            return coefficients;
        }

        // -------------------------------------------------------------------------
        // METHOD: PrintIngestedModel
        // Purpose: Prints a beautiful, clean summary of what we just parsed to the 
        // screen so the user can verify the computer understood the text file correctly.
        // -------------------------------------------------------------------------
        public static void PrintIngestedModel(LinearModel model)
        {
            // Clear the screen so this report gets full focus!
            Console.Clear();

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