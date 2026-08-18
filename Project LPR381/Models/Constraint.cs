using System.Collections.Generic;

namespace LPR381.Models
{
    // Think of this class as a single physical limitation or rule in our math problem.
    // For example, if we only have 40 hours of labor available, this class holds that boundary.
    public class Constraint
    {
        // This list holds the numbers attached to our variables (like the '3' and '4' in 3x + 4y <= 40).
        public List<double> Coefficients { get; set; } = new List<double>();

        // This is the mathematical operator. Is it less than (<=), greater than (>=), or exactly equal (=)?
        public string Relation { get; set; }

        // RHS stands for Right-Hand Side. This is our absolute limit or capacity for this specific rule (e.g., the 40).
        public double RHS { get; set; }
    }
}