// File: Models/SimplexResult.cs
using System.Collections.Generic;

namespace LPR381.Models
{
    // This is our Data Transfer Object (DTO). 
    // Instead of letting other team members accidentally mess up the base Solver Engine's memory, 
    // we package the final, optimal answers into this safe object and hand it off to them.
    public class SimplexResult
    {
        // The final, absolute best number we calculated for our objective (Z).
        public double OptimalZ { get; set; }

        // A dictionary that maps the name of the variable (like "x1" or "e1") to its final calculated value.
        public Dictionary<string, double> VariableValues { get; set; } = new Dictionary<string, double>();

        // A direct, deep-cloned copy of the final 2D array grid, safe for Member 3 and 4 to use.
        public double[,] FinalTableau { get; set; }

        // The labels for the rows and columns so we know what numbers belong to what variables.
        public string[] RowHeaders { get; set; }
        public string[] ColumnHeaders { get; set; }
    }
}