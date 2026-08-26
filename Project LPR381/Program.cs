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
using Project_LPR381.PostOptimality;

namespace LPR381
{
    // -------------------------------------------------------------------------
    // ENUM: MenuOption
    // Purpose: Acts as the master directory for the console dashboard.
    // What it does: Assigns a unique numerical identifier to each user action,
    // making the input handling structured, safe, and easily extensible.
    // -------------------------------------------------------------------------
    public enum MenuOption
    {
        Exit = 0,

        // --- LAYER 2 ALGORITHMIC SOLVERS ---
        StandardPrimalSimplex = 1,
        RevisedPrimalSimplex = 2,
        BranchAndBoundSimplex = 3,
        Knapsack = 4,
        CuttingPlane = 5,

        // --- POST-OPTIMALITY & DUALITY ---
        PostOptimality = 6,

        // --- SYSTEM / FILE MANAGEMENT ---
        ManageFile = 7,

        // --- BONUS CRITERIA (10 MARKS) ---
        NonLinearBonus = 8
    }

    class Program
    {
        // -------------------------------------------------------------------------
        // METHOD: Main
        // Purpose: Entry point for the executable (`solve.exe`).
        // What it does: Sets up the console environment, initializes memory states,
        // and coordinates the primary dashboard loop.
        // How it works: Loops continuously until the user selects Option 0 (Exit).
        // -------------------------------------------------------------------------
        [STAThread]
        static void Main(string[] args)
        {
            // Set console appearance and UTF-8 encoding for clean ASCII and box characters
            Console.Clear();
            Console.Title = "LPR381 Solver Engine v1.0 - Operations Research";
            Console.OutputEncoding = Encoding.UTF8;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Clear();

            // Main loop control flag
            bool keepRunning = true;

            // Application Memory States: Track active file and calculated optimal state
            string loadedFilePath = "None";
            bool isFileImported = false;
            LinearModel currentModel = null;

            bool isOptimalStateSaved = false;
            SimplexResult optimalState = null;

            while (keepRunning)
            {
                // Refresh dashboard header and menu options
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
                Console.WriteLine("  8. Non-Linear Programming Solver");
                Console.WriteLine("  0. Exit System");
                Console.WriteLine("  --------------------------------------------------------------");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\n  Enter command choice [0-8]: ");
                Console.ResetColor();

                string input = Console.ReadLine();

                // Safely parse the user's input to prevent application crashes
                if (Enum.TryParse(input, out MenuOption choice))
                {
                    Console.Clear();

                    switch (choice)
                    {
                        // =============================================================
                        // OPTION 7: FILE MANAGEMENT & INGESTION (LAYER 1)
                        // =============================================================
                        case MenuOption.ManageFile:
                            DrawSubHeader("FILE INGESTION & DATA INSPECTION (LAYER 1)");

                            // If a file is already loaded, allow inspection or removal
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

                            // Open Windows OpenFileDialog to browse for .txt input files
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

                            // Ingest and tokenize the model via Layer 1 Parser
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

                        // =============================================================
                        // OPTION 1: STANDARD PRIMAL SIMPLEX (LAYER 2 & 3)
                        // =============================================================
                        case MenuOption.StandardPrimalSimplex:
                            DrawSubHeader("STANDARD PRIMAL SIMPLEX SOLVER (LAYER 2 & 3)");

                            if (!isFileImported)
                            {
                                ShowNoModelError();
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

                        // =============================================================
                        // OPTION 2: REVISED PRIMAL SIMPLEX (LAYER 2 & 3)
                        // =============================================================
                        case MenuOption.RevisedPrimalSimplex:
                            DrawSubHeader("REVISED PRIMAL SIMPLEX SOLVER (LAYER 2 & 3)");

                            if (!isFileImported)
                            {
                                ShowNoModelError();
                                break;
                            }

                            try
                            {
                                ExplainSimplexProcess(currentModel, true);

                                RevisedSimplex engine = new RevisedSimplex();
                                engine.InitializeModel(currentModel);
                                engine.Solve();

                                optimalState = engine.GetResult();
                                isOptimalStateSaved = true;

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

                        // =============================================================
                        // OPTION 3: BRANCH & BOUND SIMPLEX (INTEGER PROGRAMMING)
                        // =============================================================
                        case MenuOption.BranchAndBoundSimplex:
                            DrawSubHeader("BRANCH & BOUND SIMPLEX");

                            if (!isFileImported)
                            {
                                ShowNoModelError();
                                break;
                            }

                            try
                            {
                                // Detect integer or binary sign restrictions
                                bool[] integerVariables = new bool[currentModel.SignRestrictions.Count];
                                bool hasIntegerRestriction = false;

                                for (int i = 0; i < currentModel.SignRestrictions.Count; i++)
                                {
                                    string restriction = currentModel.SignRestrictions[i].Trim().ToLower();
                                    integerVariables[i] = restriction == "int" || restriction == "bin";
                                    if (integerVariables[i]) hasIntegerRestriction = true;
                                }

                                if (!hasIntegerRestriction)
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("  [!] No integer ('int') or binary ('bin') restrictions found in the text file.");
                                    Console.WriteLine("  Branch & Bound requires integer variables. Please run Option 1 instead.");
                                    Console.ResetColor();
                                    break;
                                }

                                Console.ForegroundColor = ConsoleColor.DarkCyan;
                                Console.WriteLine($"  Model loaded: {currentModel.ObjectiveCoefficients.Count} variables, {currentModel.Constraints.Count} constraints.");
                                Console.WriteLine("  Integer-restricted variables:");
                                for (int i = 0; i < integerVariables.Length; i++)
                                {
                                    if (integerVariables[i])
                                    {
                                        Console.WriteLine($"    x{i + 1} -> {currentModel.SignRestrictions[i]}");
                                    }
                                }
                                Console.ResetColor();

                                BranchAndBound solver = new BranchAndBound();
                                optimalState = solver.Solve(currentModel, integerVariables);
                                isOptimalStateSaved = true;

                                PrintAndExportResults(loadedFilePath, optimalState, solver.IterationLog.ToString());
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
                                Console.WriteLine($"\n  [BRANCH & BOUND ERROR] {ex.Message}");
                                Console.ResetColor();
                            }
                            break;

                        // =============================================================
                        // OPTION 4: BRANCH & BOUND KNAPSACK
                        // =============================================================
                        case MenuOption.Knapsack:
                            DrawSubHeader("BRANCH & BOUND KNAPSACK");

                            if (!isFileImported)
                            {
                                ShowNoModelError();
                                break;
                            }

                            try
                            {
                                Knapsack knapsackSolver = new Knapsack();
                                optimalState = knapsackSolver.Solve(currentModel);
                                isOptimalStateSaved = true;

                                PrintAndExportResults(loadedFilePath, optimalState, knapsackSolver.IterationLog.ToString());
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
                                Console.WriteLine($"\n  [KNAPSACK ERROR] {ex.Message}");
                                Console.ResetColor();
                            }
                            break;

                        // =============================================================
                        // OPTION 5: GOMORY CUTTING PLANE ALGORITHM
                        // =============================================================
                        case MenuOption.CuttingPlane:
                            DrawSubHeader("GOMORY CUTTING PLANE ALGORITHM");

                            if (!isFileImported)
                            {
                                ShowNoModelError();
                                break;
                            }

                            try
                            {
                                CuttingPlane cpSolver = new CuttingPlane();
                                optimalState = cpSolver.Solve(currentModel);
                                isOptimalStateSaved = true;

                                PrintAndExportResults(loadedFilePath, optimalState, cpSolver.IterationLog.ToString());
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\n  [CUTTING PLANE ERROR] {ex.Message}");
                                Console.ResetColor();
                            }
                            break;

                        // =============================================================
                        // OPTION 6: POST-OPTIMALITY (SENSITIVITY ANALYSIS & DUALITY)
                        // =============================================================
                        case MenuOption.PostOptimality:
                            if (!isFileImported)
                            {
                                ShowNoModelError();
                                break;
                            }

                            try
                            {
                                bool showPostOptMenu = true;
                                while (showPostOptMenu)
                                {
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
                                            if (!isOptimalStateSaved || optimalState == null)
                                            {
                                                Console.ForegroundColor = ConsoleColor.Red;
                                                Console.WriteLine("\n  [!] Optimal state not found!");
                                                Console.WriteLine("  Please run a Solver (Option 1, 2, 3, 4, or 5) first.");
                                                Console.ResetColor();
                                                Console.WriteLine("\n  Press any key to continue...");
                                                Console.ReadKey();
                                            }
                                            else
                                            {
                                                var sensitivity = new SensitivityAnalysis(optimalState, currentModel);
                                                sensitivity.ShowMenu();
                                            }
                                            break;

                                        case "2":
                                            var duality = new Duality(currentModel);
                                            duality.ShowMenu();
                                            break;

                                        case "0":
                                            showPostOptMenu = false;
                                            break;

                                        default:
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine("\n  [!] Invalid option. Please select 0, 1, or 2.");
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

                        // =============================================================
                        // OPTION 8: NON-LINEAR OPTIMIZATION (10 MARKS BONUS)
                        // =============================================================
                        case MenuOption.NonLinearBonus:
                            DrawSubHeader("GENERAL NON-LINEAR OPTIMIZATION SOLVER (10 MARKS BONUS)");
                            Console.WriteLine("  This module solves ANY custom non-linear function using Finite-Difference Gradient Descent.\n");
                            Console.WriteLine("  [1] Solve project default example: Minimize f(x) = x^2");
                            Console.WriteLine("  [2] Enter your own custom non-linear equation (e.g. x^3 - 4*x + 2, x^4 - 2*x^2, sin(x) + x^2)");
                            Console.Write("\n  Select an option [1-2]: ");

                            string nlChoice = Console.ReadLine()?.Trim();
                            NonLinearSolver solverNL = new NonLinearSolver();

                            if (nlChoice == "2")
                            {
                                try
                                {
                                    Console.Write("\n  Enter equation f(x): ");
                                    string customExpr = Console.ReadLine();

                                    Console.Write("  Goal (min / max): ");
                                    bool isMax = Console.ReadLine()?.Trim().ToLower() == "max";

                                    Console.Write("  Enter initial starting point x_0 (e.g. 3.0): ");
                                    double startX = double.TryParse(Console.ReadLine(), out double parsedX) ? parsedX : 3.0;

                                    Console.Write("  Enter learning rate alpha (e.g. 0.05): ");
                                    double alpha = double.TryParse(Console.ReadLine(), out double parsedAlpha) ? parsedAlpha : 0.05;

                                    Console.Write("  Apply boundary constraints? (Y/N): ");
                                    bool hasBounds = Console.ReadLine()?.Trim().ToUpper() == "Y";

                                    double minBound = double.NegativeInfinity;
                                    double maxBound = double.PositiveInfinity;

                                    if (hasBounds)
                                    {
                                        Console.Write("  Enter lower bound (or -inf): ");
                                        string minStr = Console.ReadLine();
                                        if (double.TryParse(minStr, out double parsedMin)) minBound = parsedMin;

                                        Console.Write("  Enter upper bound (or inf): ");
                                        string maxStr = Console.ReadLine();
                                        if (double.TryParse(maxStr, out double parsedMax)) maxBound = parsedMax;
                                    }

                                    solverNL.SolveExpression(customExpr, isMax, startX, alpha, 50, minBound, maxBound);
                                }
                                catch (Exception ex)
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"\n  [INPUT ERROR] {ex.Message}");
                                    Console.ResetColor();
                                }
                            }
                            else
                            {
                                // Solves project example f(x) = x^2, minimizing from initial x_0 = 4.0
                                solverNL.SolveExpression("x^2", false, startingX: 4.0, learningRate: 0.1);
                            }
                            break;

                        // =============================================================
                        // OPTION 0: GRACEFUL SHUTDOWN
                        // =============================================================
                        case MenuOption.Exit:
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("\n  Terminating LPR381 Solver Engine. Goodbye!\n");
                            Console.ResetColor();
                            keepRunning = false;
                            break;

                        default:
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("\n  [!] Invalid option entered.");
                            Console.ResetColor();
                            break;
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n  [!] Invalid input. Please enter a valid menu digit (0-8).");
                    Console.ResetColor();
                }

                // Pause before refreshing dashboard
                if (keepRunning)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("\n  Press any key to return to dashboard menu...");
                    Console.ReadKey();
                }
            }
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: ShowNoModelError
        // Purpose: Standardized warning when an algorithm is run without a loaded file.
        // -------------------------------------------------------------------------
        static void ShowNoModelError()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  [!] No linear model loaded in memory.");
            Console.WriteLine("  Please select Option 7 first to upload an input file.");
            Console.ResetColor();
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: PrintAndExportResults
        // Purpose: Displays the optimal solution summary and triggers Layer 3 export.
        // -------------------------------------------------------------------------
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

            // Export to Downloads folder via Layer 3 Exporter
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

        // -------------------------------------------------------------------------
        // HELPER METHOD: DrawHeader
        // Purpose: Renders the dynamic top header bar displaying active model & state.
        // -------------------------------------------------------------------------
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

        // -------------------------------------------------------------------------
        // HELPER METHOD: DrawSubHeader
        // Purpose: Prints a standardized title header for individual sub-screens.
        // -------------------------------------------------------------------------
        static void DrawSubHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  --------------------------------------------------------------");
            Console.WriteLine($"   {title}");
            Console.WriteLine($"  --------------------------------------------------------------\n");
            Console.ResetColor();
        }

        // -------------------------------------------------------------------------
        // HELPER METHOD: ExplainSimplexProcess
        // Purpose: Displays an educational trace summary before Simplex execution.
        // -------------------------------------------------------------------------
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
                Console.WriteLine("       (Equalities split into <= and >=. Slacks/Excesses added) ");
                Console.WriteLine("    3. Price Out Phase: Calculate Simplex Multipliers (Pi)      ");
                Console.WriteLine("       and solve for entering variable via Reduced Costs.       ");
                Console.WriteLine("    4. Product Form: Compute entering column (d = B^-1 * A_j).  ");
                Console.WriteLine("    5. Ratio Test: Min(x_B / d) finds leaving variable.         ");
                Console.WriteLine("    6. Pivot Update: Multiply Eta matrix to update B^-1.        ");
            }
            else
            {
                Console.WriteLine("    2. Canonical Conversion: Setup strictly Slack & Excess.     ");
                Console.WriteLine("       (Equalities split into <= and >=. No Artificials used)   ");
                Console.WriteLine("    3. Pricing Out: Search Z-Row for most negative coefficient. ");
                Console.WriteLine("    4. Minimum Ratio Test: Min(RHS / Pivot_Col) for leaving var.");
                Console.WriteLine("    5. Jordan Pivoting: Normalizes row and zeroes column.       ");
            }

            Console.WriteLine("  +------------------------------------------------------------+\n");
            Console.ResetColor();
        }
    }
}