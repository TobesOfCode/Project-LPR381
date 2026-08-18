using System.Collections.Generic;

namespace LPR381.Models
{
    public class Constraint
    {
        public List<double> Coefficients { get; set; } = new List<double>();
        public string Relation { get; set; }
        public double RHS { get; set; }
    }
}