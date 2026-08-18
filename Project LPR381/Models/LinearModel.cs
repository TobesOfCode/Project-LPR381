using System.Collections.Generic;

namespace LPR381.Models
{
    // Purpose: Master data container holding the complete parsed linear programming model attributes.
    // Use case: Passed between software layers to provide objective functions, constraints, and sign restrictions to solvers.
    public class LinearModel
    {
        public string OptimizationType { get; set; } 
        public List<double> ObjectiveCoefficients { get; set; } = new List<double>();
        public List<Constraint> Constraints { get; set; } = new List<Constraint>();
        public List<string> SignRestrictions { get; set; } = new List<string>();
    }
}