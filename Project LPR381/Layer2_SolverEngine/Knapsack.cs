using LPR381.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LPR381.Layer2_SolverEngine
{
    /// <summary>
    /// Solves a 0/1 Knapsack problem using Branch and Bound.
    ///
    /// Expected mathematical form:
    ///
    /// MAX Z = p1x1 + p2x2 + ... + pnxn
    ///
    /// Subject to:
    /// w1x1 + w2x2 + ... + wnxn <= Capacity
    ///
    /// xi = bin
    /// </summary>
    internal class Knapsack
    {
        // Stores the complete Branch & Bound Knapsack trace
        // so Tobie's existing Exporter can write it to a text file.
        public StringBuilder IterationLog { get; private set; }
            = new StringBuilder();

        // Internal list of items sorted by value / weight ratio.
        private List<KnapsackItem> items;

        // Best feasible integer solution found so far.
        private double incumbentObjectiveValue;

        // Decision-variable values belonging to the incumbent.
        private double[] incumbentVariableValues;

        // Indicates whether at least one feasible solution
        // has been found.
        private bool hasIncumbent;

        // Floating-point tolerance.
        private const double Tolerance = 0.000001;


        /// <summary>
        /// Represents one item in the Knapsack problem.
        /// </summary>
        private class KnapsackItem
        {
            // Original variable position.
            //
            // 0 = x1
            // 1 = x2
            // 2 = x3
            public int OriginalIndex { get; set; }

            // Objective coefficient / profit / value.
            public double Value { get; set; }

            // Weight used in the capacity constraint.
            public double Weight { get; set; }

            // Value divided by weight.
            public double Ratio { get; set; }
        }


        /// <summary>
        /// Represents one sub-problem in the
        /// Branch & Bound Knapsack tree.
        /// </summary>
        private class KnapsackSubProblem
        {
            // Hierarchical Branch & Bound identifier.
            //
            // Root:
            // 1
            //
            // Children:
            // 1.1
            // 1.2
            //
            // Grandchildren:
            // 1.1.1
            // 1.1.2
            public string SubProblemId { get; set; }

            // Depth in the Branch & Bound tree.
            public int Depth { get; set; }

            // Position of the next item in the
            // SORTED item list.
            public int Level { get; set; }

            // Objective value of all currently
            // selected items.
            public double ObjectiveValue { get; set; }

            // Weight of all currently selected items.
            public double CurrentWeight { get; set; }

            // Fractional Knapsack upper bound.
            public double UpperBound { get; set; }

            // Decisions stored in ORIGINAL variable order.
            //
            // -1 = not decided yet
            //  0 = excluded
            //  1 = included
            public int[] Decisions { get; set; }

            // Description of the branch that created
            // this sub-problem.
            //
            // Example:
            // x3 = 1
            // x3 = 0
            public string BranchDescription { get; set; }
        }


        /// <summary>
        /// Constructor.
        /// </summary>
        public Knapsack()
        {
            items =
                new List<KnapsackItem>();

            incumbentObjectiveValue =
                0.0;

            incumbentVariableValues =
                new double[0];

            hasIncumbent =
                false;
        }


        /// <summary>
        /// Writes output to both the console
        /// and the export IterationLog.
        /// </summary>
        private void LogLine(
            string message = "")
        {
            Console.WriteLine(message);

            IterationLog.AppendLine(message);
        }


        /// <summary>
        /// Main entry point for the 0/1
        /// Branch & Bound Knapsack algorithm.
        ///
        /// Returns a SimplexResult so that
        /// Tobie's existing Exporter can be reused.
        /// </summary>
        public SimplexResult Solve(
            LinearModel model)
        {
            // Start with a completely clean solver.
            Reset();

            // Ensure this is actually a valid
            // 0/1 Knapsack problem.
            ValidateModel(model);

            // Build and sort the Knapsack items.
            BuildItems(model);

            double capacity =
                model.Constraints[0].RHS;


            // ==========================================
            // HEADER
            // ==========================================

            LogLine();

            LogLine(
                "=============================================="
            );

            LogLine(
                "       BRANCH & BOUND KNAPSACK"
            );

            LogLine(
                "=============================================="
            );

            LogLine(
                $"Items: {items.Count}"
            );

            LogLine(
                $"Capacity: {capacity:0.###}"
            );


            // ==========================================
            // DISPLAY SORTED ITEMS
            // ==========================================

            LogLine();

            LogLine(
                "Items sorted by value / weight ratio:"
            );

            for (int i = 0;
                 i < items.Count;
                 i++)
            {
                KnapsackItem item =
                    items[i];

                LogLine(
                    $"x{item.OriginalIndex + 1}: " +
                    $"Value = {item.Value:0.###}, " +
                    $"Weight = {item.Weight:0.###}, " +
                    $"Ratio = {item.Ratio:0.###}"
                );
            }


            // ==========================================
            // CREATE ROOT SUB-PROBLEM
            // ==========================================

            KnapsackSubProblem root =
                new KnapsackSubProblem();

            root.SubProblemId =
                "1";

            root.Depth =
                0;

            root.Level =
                0;

            root.ObjectiveValue =
                0.0;

            root.CurrentWeight =
                0.0;

            root.Decisions =
                Enumerable.Repeat(
                    -1,
                    model.ObjectiveCoefficients.Count
                ).ToArray();

            root.BranchDescription =
                "Root problem";

            root.UpperBound =
                CalculateUpperBound(
                    root,
                    capacity
                );


            LogLine();

            LogLine(
                $"Root Upper Bound: {root.UpperBound:0.###}"
            );


            // ==========================================
            // START RECURSIVE BRANCH & BOUND
            // ==========================================

            ExploreSubProblem(  root,   capacity   );


            // ==========================================
            // PACKAGE FINAL RESULT
            // ==========================================

            SimplexResult result =
                new SimplexResult();


            LogLine();

            LogLine(
                "=============================================="
            );

            LogLine(
                "     BRANCH & BOUND KNAPSACK"
            );

            LogLine(
                "       FINAL INTEGER SOLUTION"
            );

            LogLine(
                "=============================================="
            );


            if (hasIncumbent)
            {
                result.OptimalZ =
                    incumbentObjectiveValue;


                LogLine(
                    $"Optimal Objective Value: " +
                    $"{incumbentObjectiveValue:0.###}"
                );


                LogLine();

                LogLine(
                    "Decision Variables:"
                );


                for (int i = 0;
                     i < incumbentVariableValues.Length;
                     i++)
                {
                    result.VariableValues[
                        $"x{i + 1}"
                    ] =
                        incumbentVariableValues[i];


                    LogLine(
                        $"x{i + 1} = " +
                        $"{incumbentVariableValues[i]:0}"
                    );
                }


                // Calculate final used capacity.
                double finalWeight =
                    0.0;


                for (int i = 0;
                     i < incumbentVariableValues.Length;
                     i++)
                {
                    finalWeight +=
                        incumbentVariableValues[i] *
                        model
                            .Constraints[0]
                            .Coefficients[i];
                }


                LogLine();

                LogLine(
                    $"Total Weight: {finalWeight:0.###}"
                );

                LogLine(
                    $"Capacity: {capacity:0.###}"
                );
            }
            else
            {
                result.OptimalZ =
                    0.0;


                for (int i = 0;
                     i < model.ObjectiveCoefficients.Count;
                     i++)
                {
                    result.VariableValues[
                        $"x{i + 1}"
                    ] = 0.0;
                }


                LogLine(
                    "No feasible integer solution was found."
                );
            }


            LogLine(
                "=============================================="
            );


            return result;
        }


        /// <summary>
        /// Clears all values left from a previous run.
        /// </summary>
        private void Reset()
        {
            IterationLog.Clear();

            items.Clear();

            incumbentObjectiveValue =
                0.0;

            incumbentVariableValues =
                new double[0];

            hasIncumbent =
                false;
        }


        /// <summary>
        /// Ensures the LinearModel is suitable for the
        /// 0/1 Knapsack Branch & Bound algorithm.
        /// </summary>
        private void ValidateModel(
            LinearModel model)
        {
            if (model == null)
            {
                throw new InvalidOperationException(
                    "No LinearModel was supplied to the Knapsack solver."
                );
            }


            // This implementation solves MAX Knapsack.
            if (model.OptimizationType
                    .Trim()
                    .ToLower() != "max")
            {
                throw new InvalidOperationException(
                    "Branch & Bound Knapsack requires a maximization problem."
                );
            }


            // Standard 0/1 Knapsack has one
            // capacity constraint.
            if (model.Constraints == null ||
                model.Constraints.Count != 1)
            {
                throw new InvalidOperationException(
                    "A standard 0/1 Knapsack model must contain exactly one capacity constraint."
                );
            }


            Constraint capacityConstraint =
                model.Constraints[0];


            if (capacityConstraint.Relation != "<=")
            {
                throw new InvalidOperationException(
                    "The Knapsack capacity constraint must use <=."
                );
            }


            if (capacityConstraint.RHS < 0)
            {
                throw new InvalidOperationException(
                    "Knapsack capacity cannot be negative."
                );
            }


            int numberOfVariables =
                model.ObjectiveCoefficients.Count;


            if (capacityConstraint
                    .Coefficients.Count
                != numberOfVariables)
            {
                throw new InvalidOperationException(
                    "The number of Knapsack weights does not match the number of decision variables."
                );
            }


            if (model.SignRestrictions.Count
                != numberOfVariables)
            {
                throw new InvalidOperationException(
                    "A sign restriction is required for every Knapsack variable."
                );
            }


            for (int i = 0;
                 i < numberOfVariables;
                 i++)
            {
                string restriction =
                    model.SignRestrictions[i]
                        .Trim()
                        .ToLower();


                // 0/1 Knapsack requires binary variables.
                if (restriction != "bin")
                {
                    throw new InvalidOperationException(
                        $"x{i + 1} must be binary for the 0/1 Knapsack solver."
                    );
                }


                if (capacityConstraint
                        .Coefficients[i] < 0)
                {
                    throw new InvalidOperationException(
                        $"The weight of x{i + 1} cannot be negative."
                    );
                }


                if (model
                        .ObjectiveCoefficients[i] < 0)
                {
                    throw new InvalidOperationException(
                        $"The value of x{i + 1} cannot be negative in the current Knapsack implementation."
                    );
                }
            }
        }


        /// <summary>
        /// Converts the LinearModel into a list of
        /// Knapsack items and sorts them by descending
        /// value / weight ratio.
        ///
        /// This order is used to calculate the
        /// Fractional Knapsack upper bound.
        /// </summary>
        private void BuildItems(
            LinearModel model)
        {
            Constraint capacityConstraint =
                model.Constraints[0];


            for (int i = 0;
                 i < model.ObjectiveCoefficients.Count;
                 i++)
            {
                double value =
                    model.ObjectiveCoefficients[i];

                double weight =
                    capacityConstraint
                        .Coefficients[i];


                double ratio;


                // Avoid division by zero.
                if (Math.Abs(weight)
                    <= Tolerance)
                {
                    ratio =
                        value > 0
                            ? double.PositiveInfinity
                            : 0.0;
                }
                else
                {
                    ratio =
                        value / weight;
                }


                items.Add(
                    new KnapsackItem
                    {
                        OriginalIndex = i,

                        Value =
                            value,

                        Weight =
                            weight,

                        Ratio =
                            ratio
                    }
                );
            }


            // Highest value-per-weight item first.
            items =
                items
                    .OrderByDescending( item => item.Ratio ).ToList();
        }


        /// <summary>
        /// Recursively explores one Branch & Bound
        /// Knapsack sub-problem.
        /// </summary>
        private void ExploreSubProblem(
            KnapsackSubProblem subProblem,
            double capacity)
        {
            LogLine();

            LogLine(
                "----------------------------------------------"
            );


            LogLine(   $"Exploring Sub-Problem " +  $"{subProblem.SubProblemId}"
            );


            LogLine($"Branch: {subProblem.BranchDescription}" );


            LogLine(
                $"Objective Value: " +
                $"{subProblem.ObjectiveValue:0.###}"
            );


            LogLine(
                $"Current Weight: " +
                $"{subProblem.CurrentWeight:0.###}"
            );


            LogLine(
                $"Upper Bound: " +
                $"{subProblem.UpperBound:0.###}"
            );


            // ==========================================
            // FATHOM BY INFEASIBILITY
            // ==========================================

            if (subProblem.CurrentWeight
                > capacity + Tolerance)
            {
                LogLine(
                    $"Sub-Problem " +
                    $"{subProblem.SubProblemId} " +
                    $"fathomed: Capacity exceeded."
                );

                return;
            }


            // Every feasible partial selection represents
            // a valid 0/1 candidate if all undecided
            // variables are treated as zero.
            UpdateIncumbent(
                subProblem
            );


            // ==========================================
            // FATHOM BY BOUND
            // ==========================================

            if (hasIncumbent &&
                subProblem.UpperBound
                    <= incumbentObjectiveValue + Tolerance)
            {
                LogLine(
                    $"Sub-Problem " +
                    $"{subProblem.SubProblemId} fathomed: " +
                    $"Bound {subProblem.UpperBound:0.###} " +
                    $"cannot beat incumbent " +
                    $"{incumbentObjectiveValue:0.###}."
                );

                return;
            }


            // ==========================================
            // FATHOM BY COMPLETE INTEGER SOLUTION
            // ==========================================

            if (subProblem.Level
                >= items.Count)
            {
                LogLine(
                    $"Sub-Problem " +
                    $"{subProblem.SubProblemId} " +
                    $"fathomed: Complete integer solution."
                );

                return;
            }


            // Get the next item according to
            // the value / weight ordering.
            KnapsackItem currentItem =
                items[subProblem.Level];


            LogLine(
                $"Fractional decision / branching variable: " +
                $"x{currentItem.OriginalIndex + 1}"
            );


            // ==========================================
            // LEFT CHILD
            //
            // INCLUDE ITEM:
            // xi = 1
            // ==========================================

            KnapsackSubProblem leftChild =
                CreateChildSubProblem(
                    subProblem,
                    ".1"
                );


            leftChild.Level =
                subProblem.Level + 1;


            leftChild.ObjectiveValue =
                subProblem.ObjectiveValue +
                currentItem.Value;


            leftChild.CurrentWeight =
                subProblem.CurrentWeight +
                currentItem.Weight;


            leftChild.Decisions[
                currentItem.OriginalIndex
            ] = 1;


            leftChild.BranchDescription =
                $"x{currentItem.OriginalIndex + 1} = 1";


            leftChild.UpperBound =
                CalculateUpperBound(
                    leftChild,
                    capacity
                );


            // Depth-first search:
            // explore left branch first.
            ExploreSubProblem(
                leftChild,
                capacity
            );


            // ==========================================
            // RIGHT CHILD
            //
            // EXCLUDE ITEM:
            // xi = 0
            // ==========================================

            KnapsackSubProblem rightChild =
                CreateChildSubProblem(
                    subProblem,
                    ".2"
                );


            rightChild.Level =
                subProblem.Level + 1;


            rightChild.ObjectiveValue =
                subProblem.ObjectiveValue;


            rightChild.CurrentWeight =
                subProblem.CurrentWeight;


            rightChild.Decisions[
                currentItem.OriginalIndex
            ] = 0;


            rightChild.BranchDescription =
                $"x{currentItem.OriginalIndex + 1} = 0";


            rightChild.UpperBound =
                CalculateUpperBound(
                    rightChild,
                    capacity
                );


            // Backtrack and explore right branch.
            ExploreSubProblem(
                rightChild,
                capacity
            );
        }


        /// <summary>
        /// Creates a child sub-problem and copies
        /// the parent's current decisions.
        ///
        /// suffix:
        /// ".1" = left child
        /// ".2" = right child
        /// </summary>
        private KnapsackSubProblem CreateChildSubProblem(
            KnapsackSubProblem parent,
            string suffix)
        {
            KnapsackSubProblem child =
                new KnapsackSubProblem();


            child.SubProblemId =
                parent.SubProblemId +
                suffix;


            child.Depth =
                parent.Depth + 1;


            child.Level =
                parent.Level;


            child.ObjectiveValue =
                parent.ObjectiveValue;


            child.CurrentWeight =
                parent.CurrentWeight;


            child.UpperBound =
                0.0;


            child.Decisions =
                (int[])parent.Decisions.Clone();


            child.BranchDescription =
                "";


            return child;
        }


        /// <summary>
        /// Calculates an optimistic upper bound using
        /// the Fractional Knapsack relaxation.
        ///
        /// Whole items are added while they fit.
        /// If the next item cannot fit completely,
        /// the remaining capacity is filled using
        /// a fraction of that item's value.
        /// </summary>
        private double CalculateUpperBound(
            KnapsackSubProblem subProblem,
            double capacity)
        {
            // An infeasible sub-problem has no
            // useful upper bound.
            if (subProblem.CurrentWeight
                > capacity + Tolerance)
            {
                return double.NegativeInfinity;
            }


            double bound =
                subProblem.ObjectiveValue;


            double totalWeight =
                subProblem.CurrentWeight;


            // Items before Level have already
            // been decided.
            for (int i = subProblem.Level;
                 i < items.Count;
                 i++)
            {
                KnapsackItem item =
                    items[i];


                // Zero-weight positive item.
                if (Math.Abs(item.Weight)
                    <= Tolerance)
                {
                    if (item.Value > 0)
                    {
                        bound +=
                            item.Value;
                    }

                    continue;
                }


                // Add the complete item if it fits.
                if (totalWeight + item.Weight
                    <= capacity + Tolerance)
                {
                    totalWeight +=
                        item.Weight;


                    bound +=
                        item.Value;
                }
                else
                {
                    // Only part of the next item fits.
                    double remainingCapacity =
                        capacity -
                        totalWeight;


                    if (remainingCapacity > 0)
                    {
                        double fraction =
                            remainingCapacity /
                            item.Weight;


                        bound +=
                            item.Value *
                            fraction;
                    }


                    // Capacity is now filled.
                    break;
                }
            }


            return bound;
        }


        /// <summary>
        /// Checks whether this feasible candidate
        /// is better than the current incumbent.
        /// </summary>
        private void UpdateIncumbent(
            KnapsackSubProblem subProblem)
        {
            bool isBetter =
                !hasIncumbent ||
                subProblem.ObjectiveValue
                    > incumbentObjectiveValue + Tolerance;


            if (!isBetter)
            {
                return;
            }


            // New best candidate found.
            hasIncumbent =
                true;


            incumbentObjectiveValue =
                subProblem.ObjectiveValue;


            incumbentVariableValues =
                new double[
                    subProblem.Decisions.Length
                ];


            // Any variable not yet decided is treated
            // as zero for this feasible candidate.
            for (int i = 0;
                 i < subProblem.Decisions.Length;
                 i++)
            {
                incumbentVariableValues[i] =
                    subProblem.Decisions[i] == 1
                        ? 1.0
                        : 0.0;
            }


            LogLine();


            LogLine(
                $"NEW INCUMBENT: " +
                $"Sub-Problem {subProblem.SubProblemId}"
            );


            LogLine(
                $"Objective Value = " +
                $"{incumbentObjectiveValue:0.000}"
            );


            LogLine(
                "Decision Variables:"
            );


            for (int i = 0;
                 i < incumbentVariableValues.Length;
                 i++)
            {
                LogLine(
                    $"x{i + 1} = " +
                    $"{incumbentVariableValues[i]:0}"
                );
            }
        }
    }
}