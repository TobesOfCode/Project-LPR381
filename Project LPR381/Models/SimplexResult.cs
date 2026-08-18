using System;
using System.Collections.Generic;

namespace LPR381.Models
{
    // Purpose: Data Transfer Object (DTO) wrapping the final solved state, optimal value, and cloned matrix tables.
    // Use case: Handed off safely to secondary team algorithms and sensitivity analyzers without corrupting base solver references.
    public class SimplexResult
    {
        public double OptimalZ { get; set; }
        public Dictionary<string, double> VariableValues { get; set; } = new Dictionary<string, double>();
        
        public double[,] FinalTableau { get; set; }
        public string[] RowHeaders { get; set; }
        public string[] ColumnHeaders { get; set; }
    }
}