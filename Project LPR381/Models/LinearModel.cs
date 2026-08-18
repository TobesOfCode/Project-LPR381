using System.Collections.Generic;

namespace LPR381.Models
{
    // This is our master blueprint. 
    // It holds all the pieces of the math problem after we read them from the text file, keeping everything neatly organized.
    public class LinearModel
    {
        // Are we trying to maximize profit ("max") or minimize costs ("min")?
        public string OptimizationType { get; set; }

        // These are the financial values or main goals in our objective function.
        public List<double> ObjectiveCoefficients { get; set; } = new List<double>();

        // This is a list of all the physical limits and rules (Constraints) we have to follow.
        public List<Constraint> Constraints { get; set; } = new List<Constraint>();

        // These are the rules at the very bottom of the text file (like 'int', 'bin', or '+').
        // They tell us what kind of numbers our variables are allowed to be.
        public List<string> SignRestrictions { get; set; } = new List<string>();
    }
}