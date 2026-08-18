using System;
using System.IO;
using System.Collections.Generic;
using LPR381.Models;

namespace LPR381.Layer1_IO
{
    // Purpose: Handles text stream ingestion of input files, validating formatting and translating raw lines into structured C# models.
    // Use case: Triggered from the console menu UI to load user-selected model text files into memory.
    public class Parser
    {
        // Purpose: Reads the input text file line-by-line using a StreamReader and parses objectives, constraints, and bin/int sign restrictions.
        // Use case: Called when a user uploads a model file to convert raw text into a structured LinearModel object.
        public static LinearModel ScanFile(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException($"The file at {filePath} could not be found.");

            LinearModel model = new LinearModel();
            List<string> rawLines = new List<string>();

            using (StreamReader sr = new StreamReader(filePath))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line)) rawLines.Add(System.Text.RegularExpressions.Regex.Replace(line.Trim(), @"\s+", " "));
                }
            }

            if (rawLines.Count < 3) throw new FormatException("Invalid file format.");

            // 1. Parse Objective Function Line
            string[] objTokens = rawLines[0].Split(' ');
            model.OptimizationType = objTokens[0].ToLower();
            model.ObjectiveCoefficients = ExtractCoefficients(objTokens, 1);

            // 2. Parse Constraint Rows
            for (int i = 1; i < rawLines.Count - 1; i++)
            {
                string[] conTokens = rawLines[i].Split(' ');
                Constraint constraint = new Constraint();

                int relationIndex = Array.FindIndex(conTokens, t => t == "<=" || t == ">=" || t == "=");
                if (relationIndex == -1) throw new FormatException($"Missing relation in constraint row {i}");

                constraint.Relation = conTokens[relationIndex];
                constraint.RHS = double.Parse(conTokens[relationIndex + 1], System.Globalization.CultureInfo.InvariantCulture);

                string[] coeffTokens = new string[relationIndex];
                Array.Copy(conTokens, coeffTokens, relationIndex);
                constraint.Coefficients = ExtractCoefficients(coeffTokens, 0);

                model.Constraints.Add(constraint);
            }

            // 3. Parse Sign Restrictions (Supporting standard symbols like '+', 'urs' as well as 'bin' / 'int')
            string[] signTokens = rawLines[rawLines.Count - 1].Split(' ');
            foreach (var token in signTokens)
            {
                model.SignRestrictions.Add(token.ToLower());
            }

            return model;
        }

        // Purpose: Parses space-separated signed tokens into structured numeric coefficient lists.
        // Use case: Helper method used internally by ScanFile to interpret objective function and constraint numbers.
        private static List<double> ExtractCoefficients(string[] tokens, int startIndex)
        {
            List<double> coefficients = new List<double>();
            for (int i = startIndex; i < tokens.Length; i++)
            {
                if (tokens[i] == "+" || tokens[i] == "-")
                {
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

        // Purpose: Prints a beautifully formatted, color-coded UI dashboard displaying all ingested model values using horizontal lines.
        // Use case: Called immediately after ScanFile() in Program.cs to verify file parsing accuracy visually.
        public static void PrintIngestedModel(LinearModel model)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  ──────────────────────────────────────────────────────────────");
            Console.WriteLine("                    PARSER INGESTION REPORT                     ");
            Console.WriteLine("  ──────────────────────────────────────────────────────────────");

            // 1. Optimization Type Badge
            Console.Write("    MODEL TYPE      : ");
            Console.ForegroundColor = model.OptimizationType == "max" ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.Write($"{model.OptimizationType.ToUpper()}\n");
            Console.ForegroundColor = ConsoleColor.Cyan;

            // 2. Objective Coefficients
            Console.WriteLine($"    OBJ COEFFICIENTS: [ {string.Join(", ", model.ObjectiveCoefficients)} ]");

            // 3. Constraints Count & List
            Console.WriteLine($"    TOTAL CONSTR.   : {model.Constraints.Count}");
            Console.WriteLine("  ──────────────────────────────────────────────────────────────");
            Console.WriteLine("    CONSTRAINTS BREAKDOWN:");

            int index = 1;
            foreach (var con in model.Constraints)
            {
                string constraintStr = $"[{string.Join(", ", con.Coefficients)}] {con.Relation} {con.RHS}";
                Console.WriteLine($"      [{index++}] {constraintStr}");
            }

            // 4. Sign & Variable Restrictions (Including int/bin tags)
            Console.WriteLine("  ──────────────────────────────────────────────────────────────");
            Console.WriteLine($"    SIGN RESTRICT.  : [ {string.Join(", ", model.SignRestrictions)} ]");

            Console.WriteLine("  ──────────────────────────────────────────────────────────────\n");
            Console.ResetColor();
        }
    }
}