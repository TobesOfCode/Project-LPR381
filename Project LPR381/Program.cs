using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using LPR381.Models;
using LPR381.Layer1_IO;
using LPR381.Layer2_SolverEngine;
using LPR381.Layer3_Exporter;

namespace LPR381
{
    // Purpose: Enumerates selectable menu actions for the console application.
    // Workload Plan: Organized neatly by team member responsibilities.
    public enum MenuOption
    {
        Exit = 0,

        // --- MEMBER 1: CORE ARCHITECT & I/O ---
        StandardPrimalSimplex = 1,
        RevisedPrimalSimplex = 2,

        // --- MEMBER 2: TREE & NODE SPECIALIST ---
        PlaceholderModule = 3, // Reserved for Branch & Bound Simplex

        // --- MEMBER 3: IP & KNAPSACK ENGINEER ---
        Knapsack = 4, // Reserved for Branch & Bound Knapsack
        CuttingPlane = 5, // Reserved for Gomory Cutting Plane

        // --- MEMBER 4: POST-OPTIMALITY ANALYST ---
        PostOptimality = 6, // Reserved for Sensitivity Analysis & Duality

        // --- SYSTEM / UTILITY ---
        ManageFile = 7
    }

    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Console.Title = "LPR381 Solver Engine v1.0 - Operations Research";
            Console.OutputEncoding = Encoding.UTF8;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Clear();

            bool keepRunning = true;

            string loadedFilePath = "None";
            bool isFileImported = false;
            LinearModel currentModel = null;

            bool isOptimalStateSaved = false;
            SimplexResult optimalState = null;

            while (keepRunning)
            {
                Console.Clear();
                DrawHeader(isFileImported, loadedFilePath, isOptimalStateSaved);

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("  7. Manage Model File (Upload / Inspect / Delete)");
                Console.WriteLine("  --------------------------------------------------------------");
                Console.WriteLine("  1. Standard Primal Simplex");
                Console.WriteLine("  2. Revised Primal Simplex");
                Console.WriteLine("  3. Branch & Bound Simplex");
                Console.WriteLine("  4. Branch & Bound Knapsack");
                Console.WriteLine("  5. Cutting Plane Algorithm");
                Console.WriteLine("  6. Post-Optimality & Sensitivity");
                Console.WriteLine("  0. Exit System");
                Console.WriteLine("  --------------------------------------------------------------");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\n  Enter command choice [0-7]: ");
                Console.ResetColor();

                string input = Console.ReadLine();

                if (Enum.TryParse(input, out MenuOption choice))
                {
                    Console.Clear();
                    switch (choice)
                    {
                        case MenuOption.ManageFile:
                            DrawSubHeader("FILE INGESTION & DATA INSPECTION (LAYER 1)");

                            if (isFileImported)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"  Active Loaded Model : {loadedFilePath}");
                                Console.WriteLine("  [1] Upload / Replace with a different .txt file");
                                Console.WriteLine("  [2] Re-inspect current model data layout");
                                Console.WriteLine("  [3] Delete model from memory");
                                Console.WriteLine("  [0] Return to Main Menu");
                                Console.Write("\n  Select an option [0-3]: ");
                                Console.ResetColor();

                                string fileAction = Console.ReadLine();

                                if (fileAction == "2")
                                {
                                    Parser.PrintIngestedModel(currentModel);
                                    break;
                                }
                                else if (fileAction == "3")
                                {
                                    currentModel = null;
                                    isFileImported = false;
                                    loadedFilePath = "None";
                                    isOptimalStateSaved = false;
                                    optimalState = null;

                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine("\n  [SUCCESS] Active model cleared from memory.");
                                    Console.ResetColor();
                                    break;
                                }
                                else if (fileAction != "1") break;
                            }

                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine("  [INFO] Opening Windows File Explorer dialogue...");
                            Console.ResetColor();

                            string inputPath = string.Empty;
                            using (OpenFileDialog openFileDialog = new OpenFileDialog())
                            {
                                openFileDialog.Title = "Select LPR381 Linear Model Text File";
                                openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                                openFileDialog.RestoreDirectory = true;

                                if (openFileDialog.ShowDialog() == DialogResult.OK) inputPath = openFileDialog.FileName;
                            }

                            if (string.IsNullOrWhiteSpace(inputPath))
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("\n  [!] File selection was cancelled.");
                                Console.ResetColor();
                                break;
                            }

                            try
                            {
                                currentModel = Parser.ScanFile(inputPath);
                                isFileImported = true;
                                loadedFilePath = inputPath;
                                isOptimalStateSaved = false;
                                optimalState = null;

                                Parser.PrintIngestedModel(currentModel);

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("  [SUCCESS] Model ingested successfully into memory!");
                                Console.ResetColor();
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\n  [PARSER ERROR] {ex.Message}");
                                Console.ResetColor();
                            }
                            break;

                        // ==========================================
                        // OPTION 1: STANDARD PRIMAL SIMPLEX
                        // ==========================================
                        case MenuOption.StandardPrimalSimplex:
                            DrawSubHeader("STANDARD PRIMAL SIMPLEX SOLVER");

                            if (!isFileImported)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("  [!] No linear model loaded in memory.");
                                Console.WriteLine("  Please select Option 7 first to upload an input file.");
                                Console.ResetColor();
                                break;
                            }

                            try
                            {
                                ExplainSimplexProcess(currentModel, false);

                                BaseSimplex engine = new BaseSimplex();
                                engine.InitializeTableau(currentModel);
                                engine.Solve();

                                optimalState = engine.GetResult();
                                isOptimalStateSaved = true;

                                PrintAndExportResults(loadedFilePath, optimalState, engine.IterationLog.ToString());
                            }
                            catch (InvalidOperationException ioe)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\n  [MODEL EXCEPTION] {ioe.Message}");
                                Console.ResetColor();
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\n  [ENGINE ERROR] {ex.Message}");
                                Console.ResetColor();
                            }
                            break;

                        // ==========================================
                        // OPTION 2: REVISED PRIMAL SIMPLEX
                        // ==========================================
                        case MenuOption.RevisedPrimalSimplex:
                            DrawSubHeader("REVISED PRIMAL SIMPLEX SOLVER (LAYER 2 & 3)");

                            if (!isFileImported)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("  [!] No linear model loaded in memory.");
                                Console.WriteLine("  Please select Option 7 first to upload an input file.");
                                Console.ResetColor();
                                break;
                            }

                            try
                            {
                                ExplainSimplexProcess(currentModel, true);

                                // Instantiate our brand new Revised Engine instead!
                                RevisedSimplex engine = new RevisedSimplex();
                                engine.InitializeModel(currentModel);
                                engine.Solve();

                                // It still returns the exact same care package object
                                optimalState = engine.GetResult();
                                isOptimalStateSaved = true;

                                // Print and save using the exact same flow
                                PrintAndExportResults(loadedFilePath, optimalState, engine.IterationLog.ToString());
                            }
                            catch (InvalidOperationException ioe)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\n  [MODEL EXCEPTION] {ioe.Message}");
                                Console.ResetColor();
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\n  [ENGINE ERROR] {ex.Message}");
                                Console.ResetColor();
                            }
                            break;

                        case MenuOption.PlaceholderModule:
                            DrawSubHeader("BRANCH & BOUND SIMPLEX (SLOT 3)");
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("  [ASSIGNMENT NOTE] Reserved for Member 2 (Tree & Node Specialist).");
                            Console.WriteLine("  Will utilize SimplexResult DTO from Layer 2 to generate sub-problem nodes.");
                            Console.ResetColor();
                            break;

                        case MenuOption.Exit:
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("\n  Terminating LPR381 Solver Engine. Goodbye!\n");
                            Console.ResetColor();
                            keepRunning = false;
                            break;

                        default:
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("\n  [!] Module under active development or invalid option entered.");
                            Console.ResetColor();
                            break;
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n  [!] Invalid input. Please enter a valid menu digit (0-7).");
                    Console.ResetColor();
                }

                if (keepRunning)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("\n  Press any key to return to dashboard menu...");
                    Console.ReadKey();
                }
            }
        }

        // Helper: Unifies the printing and exporting process to avoid repeated code.
        static void PrintAndExportResults(string loadedFilePath, SimplexResult optimalState, string iterationsLog)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n  ==============================================================");
            Console.WriteLine("                   OPTIMAL SOLUTION IDENTIFIED                  ");
            Console.WriteLine("  ==============================================================");
            Console.WriteLine($"   Optimal Objective (Z) : {optimalState.OptimalZ.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");
            Console.WriteLine("   Optimal Variable Vector:");
            foreach (var variable in optimalState.VariableValues)
            {
                Console.WriteLine($"     {variable.Key,-6} = {variable.Value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),10}");
            }
            Console.WriteLine("  ==============================================================");
            Console.ResetColor();

            string savedFilePath = Exporter.ExportResult(loadedFilePath, optimalState, iterationsLog);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n  [EXPORT SUCCESS] Output file created in verified Downloads folder:");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  >> {savedFilePath}");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("\n  Would you like to open the generated text file now? (Y/N): ");
            Console.ResetColor();
            if (Console.ReadLine()?.Trim().ToUpper() == "Y")
            {
                Process.Start(new ProcessStartInfo(savedFilePath) { UseShellExecute = true });
            }
        }

        static void DrawHeader(bool isImported, string filePath, bool isOptimalGenerated)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  +------------------------------------------------------------+");
            Console.WriteLine("  |             LPR381 - SOLVER ENGINE CONTROLLER              |");
            Console.WriteLine("  +------------------------------------------------------------+");
            Console.WriteLine("");
            Console.WriteLine("  SYSTEM STATUS & MEMORY MONITOR:                              ");

            Console.Write("  File Ingestion : ");
            if (isImported)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                string displayPath = filePath.Length > 28 ? "..." + filePath.Substring(filePath.Length - 28) : filePath;
                Console.Write($"LOADED ({displayPath})".PadRight(39));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("NO MODEL IMPORTED".PadRight(39));
            }
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("");

            Console.Write("  Tableau State  : ");
            if (isOptimalGenerated)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("OPTIMAL BASE COMPUTED".PadRight(39));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("UNSOLVED / PENDING".PadRight(39));
            }
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n");

            Console.WriteLine("  +------------------------------------------------------------+");
        }

        static void DrawSubHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  --------------------------------------------------------------");
            Console.WriteLine($"   {title}");
            Console.WriteLine($"  --------------------------------------------------------------\n");
            Console.ResetColor();
        }

        static void ExplainSimplexProcess(LinearModel model, bool isRevised)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("  +------------------------------------------------------------+");
            Console.WriteLine("  |                 MATHEMATICAL SOLVER TRACE                  |");
            Console.WriteLine("  +------------------------------------------------------------+");
            Console.WriteLine($"   1. Problem Formulation: {model.OptimizationType.ToUpper()} with {model.ObjectiveCoefficients.Count} vars, {model.Constraints.Count} rows.");

            if (isRevised)
            {
                Console.WriteLine("    2. Memory Setup: Initialize Inverse Basis Matrix (B^-1)     ");
                Console.WriteLine("    3. Price Out Phase: Calculate Simplex Multipliers (Pi)      ");
                Console.WriteLine("       and solve for entering variable via Reduced Costs.       ");
                Console.WriteLine("    4. Product Form: Compute entering column (d = B^-1 * A_j).  ");
                Console.WriteLine("    5. Ratio Test: Min(x_B / d) finds leaving variable.         ");
                Console.WriteLine("    6. Pivot Update: Multiply Eta matrix to update B^-1.        ");
            }
            else
            {
                Console.WriteLine("    2. Canonical Conversion: Adding slack variables (s1, s2...) ");
                Console.WriteLine("    3. Pricing Out: Search Z-Row for most negative coefficient.");
                Console.WriteLine("    4. Minimum Ratio Test: Min(RHS / Pivot_Col) for leaving var.");
                Console.WriteLine("    5. Jordan Pivoting: Normalizes row and zeroes column.       ");
            }

            Console.WriteLine("  +------------------------------------------------------------+\n");
            Console.ResetColor();
        }
    }
}