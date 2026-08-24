using System;
using System.Collections.Generic;

namespace Project_LPR381.Layer2_SolverEngine
{
    internal class Knapsacks
    {
        public class Node
        {
            public int Level { get; set; }
            public double Bound { get; set; }
            public double Value { get; set; }
            public double Weight { get; set; }
            public int[] Solution { get; set; } = Array.Empty<int>();
        }

        /// <summary>
        /// Member 3: Branch & Bound logic adapted specifically for 0-1 (binary) decision variables.
        /// </summary>
        public static double SolveBinaryKnapsack(double[] values, double[] weights, double capacity, out int[] bestItems)
        {
            int n = values.Length;
            bestItems = new int[n];
            double maxProfit = 0;

            Queue<Node> queue = new Queue<Node>();

            Node root = new Node
            {
                Level = -1,
                Value = 0,
                Weight = 0,
                Solution = new int[n]
            };
            root.Bound = CalculateBound(root, n, capacity, values, weights);

            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                Node current = queue.Dequeue();

                if (current.Bound <= maxProfit || current.Level == n - 1)
                    continue;

                int nextLevel = current.Level + 1;

                // --- Branch 1: Include item (x_k = 1) ---
                Node includeNode = new Node
                {
                    Level = nextLevel,
                    Weight = current.Weight + weights[nextLevel],
                    Value = current.Value + values[nextLevel],
                    Solution = (int[])current.Solution.Clone()
                };
                includeNode.Solution[nextLevel] = 1;

                if (includeNode.Weight <= capacity)
                {
                    if (includeNode.Value > maxProfit)
                    {
                        maxProfit = includeNode.Value;
                        bestItems = (int[])includeNode.Solution.Clone();
                    }

                    includeNode.Bound = CalculateBound(includeNode, n, capacity, values, weights);
                    if (includeNode.Bound > maxProfit)
                        queue.Enqueue(includeNode);
                }

                // --- Branch 2: Exclude item (x_k = 0) ---
                Node excludeNode = new Node
                {
                    Level = nextLevel,
                    Weight = current.Weight,
                    Value = current.Value,
                    Solution = (int[])current.Solution.Clone()
                };
                excludeNode.Solution[nextLevel] = 0;

                excludeNode.Bound = CalculateBound(excludeNode, n, capacity, values, weights);
                if (excludeNode.Bound > maxProfit)
                    queue.Enqueue(excludeNode);
            }

            return maxProfit;
        }

        private static double CalculateBound(Node node, int n, double capacity, double[] values, double[] weights)
        {
            if (node.Weight >= capacity) return 0;

            double bound = node.Value;
            int j = node.Level + 1;
            double totalWeight = node.Weight;

            while (j < n && totalWeight + weights[j] <= capacity)
            {
                totalWeight += weights[j];
                bound += values[j];
                j++;
            }

            if (j < n)
                bound += (capacity - totalWeight) * (values[j] / weights[j]);

            return bound;
        }
    }
}