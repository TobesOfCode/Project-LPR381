using System.Collections.Generic;

namespace LPR381.Models
{
    public class LinearModel
    {
        public string OptimizationType { get; set; }
        public List<double> ObjectiveCoefficients { get; set; } = new List<double>();
        public List<Constraint> Constraints { get; set; } = new List<Constraint>();
        public List<string> SignRestrictions { get; set; } = new List<string>();
    }
}