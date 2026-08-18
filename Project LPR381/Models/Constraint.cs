using System.Collections.Generic;

namespace LPR381.Models
{
    // Purpose: Defines individual constraint properties including coefficients, relation operator, and RHS.
    // Use case: Instantiated by the Parser layer to store each row of constraints read from the input model text file.
    public class Constraint
    {
        public List<double> Coefficients { get; set; } = new List<double>();
        public string Relation { get; set; } 
        public double RHS { get; set; }
    }
}