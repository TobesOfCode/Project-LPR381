using System;

namespace LPR381
{
    // You can keep this enum here or move it to your Models folder
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
            bool keepRunning = true;

            while (keepRunning)
            {
                Console.Clear();
                Console.WriteLine("=================================");
                Console.WriteLine("    LPR381 - Solver Engine       ");
                Console.WriteLine("=================================");
                Console.WriteLine("Please select an algorithm:");
                Console.WriteLine("1. Standard Primal Simplex");
                Console.WriteLine("2. Revised Primal Simplex");
                Console.WriteLine("3. Branch & Bound");
                Console.WriteLine("4. Knapsack");
                Console.WriteLine("5. Cutting Plane");
                Console.WriteLine("6. Post-Optimality Analysis");
                Console.WriteLine("0. Exit");
                Console.WriteLine("=================================");
                Console.Write("Enter your choice (0-6): ");

                string input = Console.ReadLine();

                if (Enum.TryParse(input, out MenuOption choice))
                {
                    switch (choice)
                    {
                        case MenuOption.StandardPrimalSimplex:
                            Console.WriteLine("\nExecuting Standard Primal Simplex...");
                            break;
                        case MenuOption.RevisedPrimalSimplex:
                            Console.WriteLine("\nExecuting Revised Primal Simplex...");
                            break;
                        case MenuOption.BranchAndBound:
                            Console.WriteLine("\nExecuting Branch & Bound...");
                            break;
                        case MenuOption.Knapsack:
                            Console.WriteLine("\nExecuting Knapsack...");
                            break;
                        case MenuOption.CuttingPlane:
                            Console.WriteLine("\nExecuting Cutting Plane...");
                            break;
                        case MenuOption.PostOptimality:
                            Console.WriteLine("\nOpening Post-Optimality Menu...");
                            break;
                        case MenuOption.Exit:
                            Console.WriteLine("\nExiting program. Goodbye!");
                            keepRunning = false;
                            break;
                        default:
                            Console.WriteLine("\nInvalid choice. Please select a valid number from the menu.");
                            break;
                    }
                }
                else
                {
                    Console.WriteLine("\nInvalid input. Please enter a number.");
                }

                if (keepRunning)
                {
                    Console.WriteLine("\nPress any key to return to the menu...");
                    Console.ReadKey();
                }
            }
        }
    }
}