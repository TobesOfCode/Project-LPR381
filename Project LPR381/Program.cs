using System;
using System.Windows.Forms;
using LPR381.Models;
using LPR381.Layer1_IO;
using LPR381.Layer2_SolverEngine;

namespace LPR381
{
    // Purpose: Enumerates available menu options for the console interface.
    // Use case: Directs user input selections inside the main application loop.
    public enum MenuOption
    {
        Exit = 0,
        StandardPrimalSimplex = 1,
        RevisedPrimalSimplex = 2,
        PlaceholderModule = 3, // Placeholder replacing Branch & Bound
        Knapsack = 4,
        CuttingPlane = 5,
        PostOptimality = 6,
        ManageFile = 7
    }

    class Program
    {
        // Purpose: Coordinates the interactive menu-driven user interface loop, managing file states and execution flows.
        // Use case: Serves as application entry point (`solve.exe`) where users select options to upload files or run solvers.
        [STAThread]
        static void Main(string[] args)
        {
            Console.Title = "LPR381 Solver Engine v1.0";
            Console.OutputEncoding = System.Text.Encoding.UTF8;
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
                Console.WriteLine("  7. Manage Model File (Upload / Delete)");
                Console.WriteLine("  ----------------------------------------------");
                Console.WriteLine("  1. Standard Primal Simplex");
                Console.WriteLine("  2. Revised Primal Simplex");
                Console.WriteLine("  3. [Placeholder Module]");
                Console.WriteLine("  4. Branch & Bound Knapsack");
                Console.WriteLine("  5. Cutting Plane Algorithm");
                Console.WriteLine("  6. Post-Optimality Analysis");
                Console.WriteLine("  0. Exit");
                Console.WriteLine(" ╚══════════════════════════════════════════════╝");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\n  Enter your command [0-7]: ");
                Console.ResetColor();

                string input = Console.ReadLine();

                if (Enum.TryParse(input, out MenuOption choice))
                {
                    Console.Clear();
                    switch (choice)
                    {
                        // Purpose: Handles file browsing dialogues to upload new model files, inspect ingested values, or clear active memory states.
                        // Use case: Triggered by option 7 on the main menu to control the active model lifecycle.
                        case MenuOption.ManageFile:
                            DrawSubHeader("MANAGE MODEL FILE");

                            if (isFileImported)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"  Currently Loaded: {loadedFilePath}");
                                Console.WriteLine("  1. Upload a different file");
                                Console.WriteLine("  2. Delete current model from memory");
                                Console.WriteLine("  0. Cancel and return to menu");
                                Console.Write("\n  Select an option: ");
                                Console.ResetColor();

                                string fileAction = Console.ReadLine();

                                if (fileAction == "2")
                                {
                                    currentModel = null;
                                    isFileImported = false;
                                    loadedFilePath = "None";
                                    isOptimalStateSaved = false;
                                    optimalState = null;

                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine("\n  [SUCCESS] Model successfully deleted from memory.");
                                    Console.ResetColor();
                                    break;
                                }
                                else if (fileAction != "1") break;
                            }

                            Console.WriteLine("\n  Opening File Explorer... Please select your input text file.");
                            string inputPath = string.Empty;

                            using (OpenFileDialog openFileDialog = new OpenFileDialog())
                            {
                                openFileDialog.Title = "Select LPR381 Model File";
                                openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                                openFileDialog.RestoreDirectory = true;

                                if (openFileDialog.ShowDialog() == DialogResult.OK) inputPath = openFileDialog.FileName;
                            }

                            if (string.IsNullOrWhiteSpace(inputPath))
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("  [!] File selection cancelled.");
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

                                // --- INSPECTION CALL: Prints all ingested tokens, coefficients, constraints, and int/bin restrictions ---
                                Parser.PrintIngestedModel(currentModel);

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"\n  [SUCCESS] Parsed {currentModel.OptimizationType.ToUpper()} problem with {currentModel.Constraints.Count} constraints!");
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\n  [ERROR] {ex.Message}");
                            }
                            Console.ResetColor();
                            break;

                        // Purpose: Executes standard primal simplex solver workflow on imported model.
                        // Use case: Triggered by option 1 to calculate and display relaxation optimization results.
                        case MenuOption.StandardPrimalSimplex:
                            DrawSubHeader("STANDARD PRIMAL SIMPLEX");

                            if (!isFileImported)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("  [!] No model loaded. Please manage files (Option 7) first.");
                                break;
                            }

                            try
                            {
                                BaseSimplex engine = new BaseSimplex();
                                engine.InitializeTableau(currentModel);
                                engine.Solve();

                                optimalState = engine.GetResult();
                                isOptimalStateSaved = true;

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("\n  [SUCCESS] Optimal Solution Found and Saved to State!");
                                Console.ResetColor();

                                Console.WriteLine($"\n  Optimal Z = {Math.Round(optimalState.OptimalZ, 3)}");
                                Console.WriteLine("  Variable Values:");
                                foreach (var variable in optimalState.VariableValues)
                                {
                                    Console.WriteLine($"  {variable.Key,4} = {Math.Round(variable.Value, 3)}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\n  [ALGORITHM ERROR] {ex.Message}");
                            }
                            break;

                        // Purpose: Placeholder slot for future algorithm expansions.
                        // Use case: Triggered by option 3.
                        case MenuOption.PlaceholderModule:
                            DrawSubHeader("PLACEHOLDER MODULE");
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("  [!] This module is currently a placeholder and under development.");
                            Console.ResetColor();
                            break;

                        // Purpose: Exits and terminates console execution lifecycle.
                        // Use case: Selected by user to safely shut down program.
                        case MenuOption.Exit:
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("\n  Shutting down Solver Engine. Goodbye!\n");
                            Console.ResetColor();
                            keepRunning = false;
                            break;

                        default:
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("\n  [!] Feature under construction or invalid choice.");
                            break;
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n  [!] Invalid input. Please enter a numerical value.");
                }

                if (keepRunning)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("\n  Press any key to return to the main menu...");
                    Console.ReadKey();
                }
            }
        }

        // Purpose: Draws dashboard header including dynamic file status and optimal state metrics.
        // Use case: Refreshed on every loop iteration to give visual feedback on application state.
        static void DrawHeader(bool isImported, string filePath, bool isOptimalGenerated)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n ╔══════════════════════════════════════════════╗");
            Console.WriteLine(" ║          LPR381 - SOLVER ENGINE              ║");
            Console.WriteLine(" ╠══════════════════════════════════════════════╣");
            Console.WriteLine(" ║  Select an algorithm to process the model:   ║");
            Console.WriteLine(" ║                                              ║");

            Console.Write(" ║  FILE STATUS: ");
            if (isImported)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                string displayPath = filePath.Length > 25 ? "..." + filePath.Substring(filePath.Length - 25) : filePath;
                Console.Write($"LOADED ({displayPath})".PadRight(31));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("NOT IMPORTED".PadRight(31));
            }
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("║");

            Console.Write(" ║  OPTIMAL STATE: ");
            if (isOptimalGenerated)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("GENERATED".PadRight(29));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("PENDING".PadRight(29));
            }
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("║");

            Console.WriteLine(" ╠══════════════════════════════════════════════╣");
        }

        // Purpose: Prints a clean, color-coded sub-header title for individual menu operations.
        // Use case: Called when switching into different algorithm or file management screens.
        static void DrawSubHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  === {title} ===");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}