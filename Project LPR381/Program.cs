// File: Program.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using LPR381.Models;
using LPR381.Layer1_IO;
using LPR381.Layer2_SolverEngine;
using LPR381.Layer3_Exporter;
using Project_LPR381.PostOptimality; // Added Member 4 Post-Optimality namespace

namespace LPR381
{
    // Purpose: This list acts as our menu's table of contents. 
    // It assigns a specific number to each team member's job so the system knows exactly what to run.
    // Workload Plan: Organized neatly by team member responsibilities so no one trips over each other.
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
        PostOptimality = 6, // Sensitivity Analysis & Duality Module

        // --- SYSTEM / UTILITY ---
        ManageFile = 7
    }

    class Program
    {
        // This is the front door of our application. 
        // When you run 'solve.exe', the computer starts executing right here.
        [STAThread]
        static void Main(string[] args)
        {
            // First things first: wipe the screen clean and set up the console's title and colors.
            Console.Clear();
            Console.Title = "LPR381 Solver Engine v1.0 - Operations Research";
            Console.OutputEncoding = Encoding.UTF8;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Clear();

            // This boolean acts as the power switch for our application.
            bool keepRunning = true;

            // These variables are our system's memory. 
            // They remember if a file has been uploaded and if we've successfully solved a problem yet.
            string loadedFilePath = "None";
            bool isFileImported = false;
            LinearModel currentModel = null;

            bool isOptimalStateSaved = false;
            SimplexResult optimalState = null;

            // This is our main dashboard loop. It keeps the program running endlessly until 'keepRunning' becomes false.
            while (keepRunning)
            {
                // Wipe the screen clean every time the main menu loops so it doesn't get cluttered!
                Console.Clear();
                DrawHeader(isFileImported, loadedFilePath, isOptimalStateSaved);

                // Print out the menu options for the user to read.
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("  7. Manage Model File (Upload / Inspect / Delete)");
                Console.WriteLine("  --------------------------------------------------------------");
                Console.WriteLine("  1. Standard Primal Simplex");
                Console.WriteLine("  2. Revised Primal Simplex");
                Console.WriteLine("  3. Branch & Bound Simplex");
                Console.WriteLine("  4. Branch & Bound Knapsack");
                Console.WriteLine("  5. Cutting Plane Algorithm");
                Console.WriteLine("  6. Post-Optimality & Sensitivity (Analysis & Duality)");
                Console.WriteLine("  0. Exit System");
                Console.WriteLine("  --------------------------------------------------------------");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\n  Enter command choice [0-7]: ");
                Console.ResetColor();

                // Wait for the user to type something and press Enter.
                string input = Console.ReadLine();

                // Try to convert what the user typed into one of our MenuOption enum numbers.
                if (Enum.TryParse(input, out MenuOption choice))
                {
                    // As soon as a valid menu option is chosen, clear the screen for the new content!
                    Console.Clear();

                    // Like a train switchyard, this directs the user's choice to the correct block of code.
                    switch (choice)
                    {
                        // ==========================================
                        // OPTION 7: FILE MANAGEMENT
                        // ==========================================
                        case MenuOption.ManageFile:
                            DrawSubHeader("FILE INGESTION & DATA INSPECTION (LAYER 1)");

                            // If a file is already loaded, give the user a mini-menu to delete it or inspect it.
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
                                    // Wipe the memory clean.
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

                            // If no file is loaded, open the standard Windows File Explorer so they can click a .txt file.
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

                            // If they clicked 'Cancel' in the file explorer.
                            if (string.IsNullOrWhiteSpace(inputPath))
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("\n  [!] File selection was cancelled.");
                                Console.ResetColor();
                                break;
                            }

                            // Try to hand the text file over to Layer 1 (The Parser) to translate into our C# model.
                            try
                            {
                                currentModel = Parser.ScanFile(inputPath);
                                isFileImported = true;
                                loadedFilePath = inputPath;
                                isOptimalStateSaved = false; // Reset the optimal state because we have a new file
                                optimalState = null;

                                Parser.PrintIngestedModel(currentModel);

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("  [SUCCESS] Model ingested successfully into memory!");
                                Console.ResetColor();
                            }
                            catch (Exception ex)
                            {
                                // If the text file has bad formatting, we catch the error here so the program doesn't crash.
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\n  [PARSER ERROR] {ex.Message}");
                                Console.ResetColor();
                            }
                            break;

                        // ==========================================
                        // OPTION 1: STANDARD PRIMAL SIMPLEX
                        // ==========================================
                        case MenuOption.StandardPrimalSimplex:
                            DrawSubHeader("STANDARD PRIMAL SIMPLEX SOLVER (LAYER 2 & 3)");

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

                                // Hand the blueprint to our Base Engine to calculate the optimal answer.
                                BaseSimplex engine = new BaseSimplex();
                                engine.InitializeTableau(currentModel);
                                engine.Solve();

                                // Grab the 'care package' of final results and save it in memory.
                                optimalState = engine.GetResult();
                                isOptimalStateSaved = true;

                                // Call our helper method to print the answers and export to the Downloads folder.
                                PrintAndExportResults(loadedFilePath, optimalState, engine.IterationLog.ToString());
                            }
                            catch (InvalidOperationException ioe)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\n  [MODEL EXCEPTION]\n{ioe.Message}");
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

                                // Hand the blueprint to our Revised Engine (the memory-efficient one!).
                                RevisedSimplex engine = new RevisedSimplex();
                                engine.InitializeModel(currentModel);
                                engine.Solve();

                                // Grab the 'care package' of final results and save it in memory.
                                optimalState = engine.GetResult();
                                isOptimalStateSaved = true;

                                // Call our helper method to print the answers and export to the Downloads folder.
                                PrintAndExportResults(loadedFilePath, optimalState, engine.IterationLog.ToString());
                            }
                            catch (InvalidOperationException ioe)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\n  [MODEL EXCEPTION]\n{ioe.Message}");
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
                        // OPTION 3: BRANCH & BOUND
                        // ==========================================
                        case MenuOption.PlaceholderModule:
                            DrawSubHeader("BRANCH & BOUND SIMPLEX");

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
                                // Determine which variables must be integer.
                                bool[] integerVariables =
                                    new bool[currentModel.SignRestrictions.Count];

                                for (int i = 0;
                                     i < currentModel.SignRestrictions.Count;
                                     i++)
                                {
                                    string restriction =
                                        currentModel.SignRestrictions[i]
                                            .Trim()
                                            .ToLower();

                                    integerVariables[i] =
                                        restriction == "int" ||
                                        restriction == "bin";
                                }

                                Console.ForegroundColor = ConsoleColor.DarkCyan;
                                Console.WriteLine(
                                    $"  Model loaded: {currentModel.ObjectiveCoefficients.Count} variables, " +
                                    $"{currentModel.Constraints.Count} constraints."
                                );

                                Console.WriteLine(
                                    "  Integer-restricted variables:"
                                );

                                for (int i = 0;
                                     i < integerVariables.Length;
                                     i++)
                                {
                                    if (integerVariables[i])
                                    {
                                        Console.WriteLine(
                                            $"    x{i + 1} -> {currentModel.SignRestrictions[i]}"
                                        );
                                    }
                                }

                                Console.ResetColor();

                                BranchAndBound solver = new BranchAndBound();

                                optimalState = solver.Solve( currentModel,  integerVariables );

                                isOptimalStateSaved = true;

                                PrintAndExportResults( loadedFilePath, optimalState, solver.IterationLog.ToString());
                            }
                            catch (InvalidOperationException ioe)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(
                                    $"\n  [MODEL EXCEPTION] {ioe.Message}"
                                );
                                Console.ResetColor();
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(
                                    $"\n  [BRANCH & BOUND ERROR] {ex.Message}"
                                );
                                Console.ResetColor();
                            }

                            break;
                        // ==========================================
                        // OPTION 4: BRANCH & BOUND KNAPSACK
                        // ==========================================
                        case MenuOption.Knapsack:

                            DrawSubHeader(
                                "BRANCH & BOUND KNAPSACK"
                            );

                            if (!isFileImported)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Red;

                                Console.WriteLine(
                                    "  [!] No linear model loaded in memory."
                                );

                                Console.WriteLine(
                                    "  Please select Option 7 first to upload an input file."
                                );

                                Console.ResetColor();

                                break;
                            }

                            try
                            {
                                // Create the Knapsack Branch & Bound solver.
                                Knapsack knapsackSolver =
                                    new Knapsack();

                                // Solve and receive the final result
                                // in the same SimplexResult format used
                                // by the other algorithms.
                                optimalState =
                                    knapsackSolver.Solve(
                                        currentModel
                                    );

                                isOptimalStateSaved =
                                    true;

                                // Reuse Tobie's existing exporter.
                                PrintAndExportResults(
                                    loadedFilePath,
                                    optimalState,
                                    knapsackSolver.IterationLog.ToString()
                                );
                            }
                            catch (InvalidOperationException ioe)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Red;

                                Console.WriteLine(
                                    $"\n  [MODEL EXCEPTION] {ioe.Message}"
                                );

                                Console.ResetColor();
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Red;

                                Console.WriteLine(
                                    $"\n  [KNAPSACK ERROR] {ex.Message}"
                                );

                                Console.ResetColor();
                            }

                            try
                            {
                                // A mini-menu loop specifically for Post-Optimality
                                bool showPostOptMenu = true;
                                while (showPostOptMenu)
                                {
                                    // Clear the screen every time we loop back to this sub-menu
                                    Console.Clear();
                                    Console.ForegroundColor = ConsoleColor.Cyan;
                                    Console.WriteLine("\n  ╔════════════════════════════════════════════════════════╗");
                                    Console.WriteLine("  ║          POST-OPTIMALITY & SENSITIVITY                 ║");
                                    Console.WriteLine("  ╚════════════════════════════════════════════════════════╝");
                                    Console.ResetColor();

                                    Console.ForegroundColor = ConsoleColor.White;
                                    Console.WriteLine("\n  1. Sensitivity Analysis");
                                    Console.WriteLine("  2. Duality");
                                    Console.WriteLine("  0. Return to Main Menu");
                                    Console.WriteLine("  ──────────────────────────────────────────────────────────");

                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.Write("\n  Select an option [0-2]: ");
                                    Console.ResetColor();

                                    string postOptInput = Console.ReadLine();

                                    switch (postOptInput)
                                    {
                                        case "1":
                                            // Launch the Sensitivity Analysis UI
                                            var sensitivity = new SensitivityAnalysis(optimalState, currentModel);
                                            sensitivity.ShowMenu();
                                            break;
                                        case "2":
                                            // Launch the Duality UI
                                            var duality = new Duality(currentModel);
                                            duality.ShowMenu();
                                            break;
                                        case "0":
                                            // Flip the switch to leave this sub-menu and go back to the main dashboard
                                            showPostOptMenu = false;
                                            break;
                                        default:
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine("\n  [!] Invalid option.");
                                            Console.ResetColor();
                                            Console.ReadKey();
                                            break;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\n  [ANALYSIS ERROR] {ex.Message}");
                                Console.ResetColor();
                            }
                            break;

                        // ==========================================
                        // OPTION 0: EXIT
                        // ==========================================
                        case MenuOption.Exit:
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("\n  Terminating LPR381 Solver Engine. Goodbye!\n");
                            Console.ResetColor();
                            keepRunning = false; // Turns off the main dashboard loop, ending the program.
                            break;

                        default:
                            // Catch-all if they type a valid number that we just haven't built out yet.
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("\n  [!] Module under active development or invalid option entered.");
                            Console.ResetColor();
                            break;
                    }
                }
                else
                {
                    // If they typed letters instead of numbers, scold them gently.
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n  [!] Invalid input. Please enter a valid menu digit (0-7).");
                    Console.ResetColor();
                }

                // Pause the screen before we loop back around so the user can actually read the results!
                if (keepRunning)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("\n  Press any key to return to dashboard menu...");
                    Console.ReadKey();
                }
            }
        }

        // Helper Method: Prints the final optimal variables to the screen and triggers Layer 3 to save the file.
        // We use this method so we don't have to write the exact same code for both Option 1 and Option 2.
        static void PrintAndExportResults(string loadedFilePath, SimplexResult optimalState, string iterationsLog)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n  ==============================================================");
            Console.WriteLine("                   OPTIMAL SOLUTION IDENTIFIED                  ");
            Console.WriteLine("  ==============================================================");
            Console.WriteLine($"   Optimal Objective (Z) : {optimalState.OptimalZ.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");
            Console.WriteLine("   Optimal Variable Vector:");

            // Loop through our care package's dictionary and print out every variable and its answer.
            foreach (var variable in optimalState.VariableValues)
            {
                Console.WriteLine($"     {variable.Key,-6} = {variable.Value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),10}");
            }
            Console.WriteLine("  ==============================================================");
            Console.ResetColor();

            // Call Layer 3 (The Exporter) to do its magic and give us the exact path where it saved the file.
            string savedFilePath = Exporter.ExportResult(loadedFilePath, optimalState, iterationsLog);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n  [EXPORT SUCCESS] Output file created in verified Downloads folder:");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  >> {savedFilePath}");
            Console.ResetColor();

            // Give the user a handy shortcut to instantly open the text file without digging through their folders.
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("\n  Would you like to open the generated text file now? (Y/N): ");
            Console.ResetColor();
            if (Console.ReadLine()?.Trim().ToUpper() == "Y")
            {
                Process.Start(new ProcessStartInfo(savedFilePath) { UseShellExecute = true });
            }
        }

        // Helper Method: Draws the sleek UI box at the top of the dashboard.
        static void DrawHeader(bool isImported, string filePath, bool isOptimalGenerated)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  +------------------------------------------------------------+");
            Console.WriteLine("  |             LPR381 - SOLVER ENGINE CONTROLLER              |");
            Console.WriteLine("  +------------------------------------------------------------+");
            Console.WriteLine("");
            Console.WriteLine("  SYSTEM STATUS & MEMORY MONITOR:                              ");

            Console.Write("  File Ingestion : ");

            // Dynamic text that changes color depending on if a file is in our system memory.
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

            // Dynamic text that changes color depending on if we have solved the math yet.
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

        // Helper Method: Just a quick visual separator line so we don't have to type it out every time.
        static void DrawSubHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  --------------------------------------------------------------");
            Console.WriteLine($"   {title}");
            Console.WriteLine($"  --------------------------------------------------------------\n");
            Console.ResetColor();
        }

        // Helper Method: Prints a small text box explaining the mathematical steps before they run.
        static void ExplainSimplexProcess(LinearModel model, bool isRevised)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("  +------------------------------------------------------------+");
            Console.WriteLine("  |                 MATHEMATICAL SOLVER TRACE                  |");
            Console.WriteLine("  +------------------------------------------------------------+");
            Console.WriteLine($"   1. Problem Formulation: {model.OptimizationType.ToUpper()} with {model.ObjectiveCoefficients.Count} vars, {model.Constraints.Count} rows.");

            // Changes the text dynamically depending on if we are running Option 1 or Option 2.
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