// File: Models/Constraint.cs
using System.Collections.Generic;

namespace LPR381.Models
{
    // Think of this class as a single physical rule in our math problem.
    // For example, if a factory only has 40 hours of labor, this class stores that specific boundary.
    public class Constraint
    {
        // The numbers attached to our variables (like the '3' and '4' in 3x + 4y <= 40).
        public List<double> Coefficients { get; set; } = new List<double>();

        // The mathematical operator: is it less than (<=), greater than (>=), or exactly equal (=)?
        public string Relation { get; set; }

        // RHS stands for Right-Hand Side. This is our absolute limit for this rule (e.g., the 40).
        public double RHS { get; set; }
    }
}