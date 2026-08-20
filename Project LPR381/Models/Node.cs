using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LPR381.Models
{
    /// <summary>
    /// Represents one node (sub-problem) in the Branch and Bound tree.
    /// Each node stores information about the LP relaxation solved
    /// at that point in the tree.
    /// </summary>
    internal class Node
    {
        // Unique number used to identify the node.
        // Example: Node 1, Node 2, Node 3...
        public string SubProblemId { get; set; }

        // Indicates how deep this node is in the Branch & Bound tree.
        // Root node = depth 0.
        public int Depth { get; set; }

        // The node that created this node.
        // Root node will have no parent.
        public Node Parent { get; set; }

        // Left child created when branching:
        // x <= floor(fractional value)
        public Node LeftChild { get; set; }

        // Right child created when branching:
        // x >= ceiling(fractional value)
        public Node RightChild { get; set; }

        // Stores the LP objective value obtained
        // after solving this node's LP relaxation.
        public double ObjectiveValue { get; set; }

        // Stores all decision-variable values returned by Simplex.
        //
        // Example:
        // VariableValues[0] = x1
        // VariableValues[1] = x2
        // VariableValues[2] = x3
        public double[] VariableValues { get; set; }
        /// <summary>
        /// The actual LP model represented by this sub-problem.
        /// The root stores the original model.
        /// Child nodes store copies of the model with an added branch constraint.
        /// </summary>
        public LinearModel Model { get; set; }

        /// <summary>
        /// Stores the Simplex result obtained after solving this sub-problem.
        /// </summary>
        public SimplexResult SimplexResult { get; set; }

        // Has this node been solved using Simplex?
        public bool IsSolved { get; set; }

        // Is the LP relaxation feasible?
        public bool IsFeasible { get; set; }

        // Was the LP relaxation unbounded?
        public bool IsUnbounded { get; set; }

        // Has this node been removed/fathomed?
        public bool IsFathomed { get; set; }

        // Explanation of why the node was fathomed.
        //
        // Examples:
        // "Integer solution"
        // "Infeasible"
        // "Bound cannot beat incumbent"
        public string FathomReason { get; set; }

        // Stores a readable description of the branch
        // that created this node.
        //
        // Example:
        // "x2 <= 3"
        // "x2 >= 4"
        public string BranchDescription { get; set; }

        /// <summary>
        /// Stores binary variables that have been fixed by branching.
        ///
        /// Key:
        /// variable index, where 0 = x1, 1 = x2, etc.
        ///
        /// Value:
        /// 0 or 1.
        /// </summary>
        public Dictionary<int, int> FixedBinaryValues { get; set; }

        /// <summary>
        /// Constructor used when a new Branch & Bound node is created.
        /// </summary>
        public Node(string subProblemId, int depth)
        {
            SubProblemId = subProblemId;
            Depth = depth;

            Parent = null;
            LeftChild = null;
            RightChild = null;

            ObjectiveValue = 0.0;
            VariableValues = new double[0];

            IsSolved = false;
            IsFeasible = true;
            IsUnbounded = false;
            IsFathomed = false;

            FathomReason = "";
            BranchDescription = "";

            Model = null;
            SimplexResult = null;

            FixedBinaryValues = new Dictionary<int, int>();
        }

    }
}
