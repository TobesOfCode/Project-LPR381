using System;
using System.Collections.Generic;

namespace LPR381.Models
{
    // Purpose: Represents a single node in the Branch & Bound search tree holding a snapshot of the tableau and bounds.
    // Use case: Managed recursively to explore integer sub-problems, track branching history, and fathom suboptimal nodes.
    public class BBSnode
    {
        public int NodeId { get; set; }
        public double[,] Tableau { get; set; }
        public string[] RowHeaders { get; set; }
        public string[] ColumnHeaders { get; set; }
        public double ObjectiveValue { get; set; }
        public Dictionary<string, double> VariableValues { get; set; } = new Dictionary<string, double>();
        public bool IsIntegerFeasible { get; set; }
        public bool IsFathomed { get; set; }
        public string FathomReason { get; set; } = string.Empty;
    }
}