using System;

namespace LPR381
{
    public enum MenuOption
    {
        Exit = 0,
        StandardPrimalSimplex = 1,
        RevisedPrimalSimplex = 2,
        BranchAndBound = 3,
        Knapsack = 4,
        CuttingPlane = 5,
        PostOptimality = 6
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Premium Console Configuration
            Console.Title = "LPR381 Solver Engine v1.0";
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Clear();

            bool keepRunning = true;

            while (keepRunning)
            {
                Console.Clear();
                DrawHeader();

                // Menu Options
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("  1. Standard Primal Simplex");
                Console.WriteLine("  2. Revised Primal Simplex");
                Console.WriteLine("  3. Branch & Bound Simplex");
                Console.WriteLine("  4. Branch & Bound Knapsack");
                Console.WriteLine("  5. Cutting Plane Algorithm");
                Console.WriteLine("  6. Post-Optimality Analysis");
                Console.WriteLine("  0. Exit");
                Console.WriteLine(" ╚══════════════════════════════════════════════╝");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\n  Enter your command [0-6]: ");
                Console.ResetColor();

                string input = Console.ReadLine();

                if (Enum.TryParse(input, out MenuOption choice))
                {
                    Console.Clear();
                    switch (choice)
                    {
                        case MenuOption.StandardPrimalSimplex:
                            DrawSubHeader("STANDARD PRIMAL SIMPLEX");
                            // Trigger Parser and BaseSimplex here
                            break;
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
                    Console.WriteLine("\n  Press any key to return to the main menu..." );
                    Console.ReadKey();
                }
            }
        }

        // --- UI Helper Methods ---

        static void DrawHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n ╔══════════════════════════════════════════════╗");
            Console.WriteLine(" ║          LPR381 - SOLVER ENGINE              ║");
            Console.WriteLine(" ╠══════════════════════════════════════════════╣");
            Console.WriteLine(" ║  Select an algorithm to process the model:   ║");
            Console.WriteLine(" ║                                              ║");
        }

        static void DrawSubHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  === {title} ===");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}