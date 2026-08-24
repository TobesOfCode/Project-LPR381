using LPR381.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LPR381.Layer2_SolverEngine
{
    /// <summary>
    /// Controls the Branch and Bound process.
    ///
    /// This class will:
    /// - create nodes
    /// - keep track of the Branch & Bound tree
    /// - store the best integer solution (incumbent)
    /// - later call the Simplex solver for every node
    /// - branch again when fractional integer variables remain
    /// - fathom nodes that no longer need to be explored
    /// </summary>
    internal class BranchAndBound
    {
        // Keeps every node created during the Branch & Bound process.
        private List<Node> nodes;

        // Used to assign unique Node IDs:
        // Node 1, Node 2, Node 3, ...
        private int nodeCounter;

        // Indicates whether we have found at least one
        // feasible integer solution yet.
        private bool hasIncumbent;

        // Stores the objective value of the current
        // best integer solution.
        private double incumbentObjectiveValue;

        // Stores the variable values belonging to
        // the current best integer solution.
        private double[] incumbentVariableValues;

        // Stores which node produced the incumbent.
        private string incumbentNodeId;

        // Small tolerance used when checking whether
        // floating-point values are integer.
        //
        // Example:
        // 3.000000001 should effectively be treated as 3.
        private const double IntegerTolerance = 0.000001;
        /// <summary>
        /// Stores the complete Branch & Bound execution trace
        /// so it can be exported to the text report.
        /// </summary>
        public StringBuilder IterationLog { get; private set; }   = new StringBuilder();
        /// <summary>
        /// Constructor.
        /// Sets the Branch & Bound solver to its starting state.
        /// </summary>
        public BranchAndBound()
        {
            nodes = new List<Node>();

            nodeCounter = 0;

            hasIncumbent = false;

            incumbentObjectiveValue = 0.0;

            incumbentVariableValues = new double[0];

            incumbentNodeId = "";
        }

        /// <summary>
        /// Starts the Branch & Bound process using the original LinearModel.
        /// The original LP relaxation becomes Sub-Problem 1.
        /// </summary>
        public SimplexResult Solve(LinearModel model, bool[] integerVariables)
        {
            // Reset the solver state so a new problem
            // starts with a completely clean tree.
            nodes.Clear();

            nodeCounter = 0;

            hasIncumbent = false;

            incumbentObjectiveValue = 0.0;

            incumbentVariableValues =
                new double[0];

            incumbentNodeId = null;

            IterationLog.Clear();

            // Create the root of the Branch & Bound tree.
            Node rootNode = CreateNode("1", 0);

            // The root represents the original problem.
            rootNode.Model = CloneModel(model);
            // Binary variables must satisfy 0 <= x <= 1
            // in the LP relaxation.
            AddBinaryUpperBounds(rootNode.Model);

            rootNode.BranchDescription = "Root problem";

            LogLine();
            LogLine("==============================");
            LogLine("       BRANCH & BOUND");
            LogLine("==============================");

            // Solve the root LP relaxation.
            ExploreNode(rootNode, integerVariables);
            LogLine();
            LogLine("==================================");
            LogLine("     FINAL INTEGER SOLUTION");
            LogLine("==================================");

            if (hasIncumbent)
            {
                LogLine($"Best Sub-Problem: {incumbentNodeId}");

                LogLine( $"Optimal Objective Value: {incumbentObjectiveValue:0.###}"  );

                LogLine();
                LogLine("Decision Variables:");

                for (int i = 0;
                     i < incumbentVariableValues.Length;
                     i++)
                {
                    LogLine( $"x{i + 1} = {incumbentVariableValues[i]:0.###}"   );
                }
            }
            else
            {
                LogLine( "No feasible integer solution was found." );
            }

            LogLine("==================================");
            // Package the final Branch & Bound answer
            // into the same SimplexResult object used
            // by the other solver modules.
            SimplexResult finalResult =
                new SimplexResult();

            if (hasIncumbent)
            {
                finalResult.OptimalZ =
                    incumbentObjectiveValue;

                for (int i = 0;
                     i < incumbentVariableValues.Length;
                     i++)
                {
                    finalResult.VariableValues[
                        $"x{i + 1}"
                    ] = incumbentVariableValues[i];
                }
            }

            return finalResult;
        }
        private void LogLine(string message = "")
        {
            Console.WriteLine(message);
            IterationLog.AppendLine(message);
        }

        /// <summary>
        /// Creates a new node and adds it to the tree.
        /// </summary>
        private Node CreateNode( string subProblemId, int depth)
        {
            Node newNode = new Node(   subProblemId,  depth);

            nodes.Add(newNode);

            return newNode;
        }

        /// <summary>
        /// Checks whether a value is effectively an integer.
        /// </summary>
        private bool IsInteger(double value)
        {
            return Math.Abs(value - Math.Round(value)) <= IntegerTolerance;
        }

        /// <summary>
        /// Finds the first decision variable that should be integer
        /// but currently has a fractional value.
        ///
        /// Returns:
        /// 0 for x1
        /// 1 for x2
        /// 2 for x3
        /// etc.
        ///
        /// Returns -1 if all required integer variables are integer.
        /// </summary>
        private int SelectFractionalVariable( double[] variableValues,bool[] integerVariables)
        {
            for (int i = 0; i < variableValues.Length; i++)
            {
                // Only check variables that are required to be integer.
                if (integerVariables[i])
                {
                    if (!IsInteger(variableValues[i]))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Calculates the lower and upper branch values
        /// for a fractional decision variable.
        ///
        /// Example:
        /// If x2 = 2.750:
        /// lower branch = x2 <= 2
        /// upper branch = x2 >= 3
        /// </summary>
        private void GetBranchValues(     double fractionalValue,  out double lowerBranchValue,out double upperBranchValue)
        {
            lowerBranchValue = Math.Floor(fractionalValue);
            upperBranchValue = Math.Ceiling(fractionalValue);
        }
        /// <summary>
        /// Creates the left and right child nodes for a fractional variable.
        ///
        /// Example:
        /// If x2 = 2.750:
        /// Left child  -> x2 <= 2
        /// Right child -> x2 >= 3
        /// </summary>
        private void CreateBranches(Node parentNode, int variableIndex, double fractionalValue)
        {
            GetBranchValues(fractionalValue, out double lowerBranchValue, out double upperBranchValue);
            string variableRestriction = parentNode.Model.SignRestrictions[variableIndex].Trim().ToLower();
            bool isBinary = variableRestriction == "bin";
            if (isBinary)
            {
                lowerBranchValue = 0;
                upperBranchValue = 1;
            }

            // -----------------------------------------
            // LEFT CHILD
            // x <= floor(fractional value)
            // -----------------------------------------

            Node leftChild = CreateNode( parentNode.SubProblemId + ".1", parentNode.Depth + 1);

            leftChild.Parent = parentNode;

            leftChild.BranchDescription = $"x{variableIndex + 1} <= " +  $"{lowerBranchValue:0.###}";

            // Copy the parent's LP model.
            leftChild.Model = CloneModel(parentNode.Model);
            leftChild.FixedBinaryValues =  new Dictionary<int, int>(   parentNode.FixedBinaryValues );

            if (isBinary)
            {
                leftChild.FixedBinaryValues[variableIndex] = 0;
            }

            // Create the actual mathematical
            // x <= floor(value) constraint.
            Constraint leftConstraint =   CreateBranchConstraint(     leftChild.Model.ObjectiveCoefficients.Count,    variableIndex,    "<=",   lowerBranchValue   );

            leftChild.Model.Constraints.Add(  leftConstraint   );


            // -----------------------------------------
            // RIGHT CHILD
            // x >= ceiling(fractional value)
            // -----------------------------------------

            Node rightChild =  CreateNode( parentNode.SubProblemId + ".2", parentNode.Depth + 1  );

            rightChild.Parent = parentNode;

            rightChild.BranchDescription =       $"x{variableIndex + 1} >= " +        $"{upperBranchValue:0.###}";

            // Separate copy of the parent model.
            rightChild.Model = CloneModel(parentNode.Model);
            rightChild.FixedBinaryValues = new Dictionary<int, int>(parentNode.FixedBinaryValues );

            if (isBinary)
            {
                rightChild.FixedBinaryValues[variableIndex] = 1;
            }

            Constraint rightConstraint = CreateBranchConstraint(rightChild.Model.ObjectiveCoefficients.Count, variableIndex, ">=",  upperBranchValue );

            rightChild.Model.Constraints.Add( rightConstraint );


            // Connect the tree.
            parentNode.LeftChild = leftChild;
            parentNode.RightChild = rightChild;
        }

        /// <summary>
        /// Creates a deep copy of a LinearModel.
        ///
        /// We need a separate model for every Branch & Bound sub-problem
        /// so that adding a branch constraint to one node does not modify
        /// its parent or sibling.
        /// </summary>
        private LinearModel CloneModel(LinearModel originalModel)
        {
            LinearModel copy = new LinearModel();

            copy.OptimizationType =  originalModel.OptimizationType;

            copy.ObjectiveCoefficients =  new List<double>( originalModel.ObjectiveCoefficients    );

            copy.SignRestrictions =  new List<string>(  originalModel.SignRestrictions   );

            foreach (Constraint originalConstraint
                     in originalModel.Constraints)
            {
                Constraint constraintCopy =  new Constraint();

                constraintCopy.Coefficients = new List<double>(  originalConstraint.Coefficients );

                constraintCopy.Relation = originalConstraint.Relation;

                constraintCopy.RHS =   originalConstraint.RHS;

                copy.Constraints.Add(  constraintCopy );
            }

            return copy;
        }

        /// <summary>
        /// Creates a Simplex-compatible model for a Branch & Bound node.
        ///
        /// Binary variables fixed to 1 are substituted into the model.
        /// This avoids sending unsupported >= branch constraints
        /// into the current BaseSimplex implementation.
        /// </summary>
        private LinearModel PrepareModelForSimplex(  Node node, out double objectiveOffset)
        {
            LinearModel preparedModel =  CloneModel(node.Model);

            objectiveOffset = 0.0;

            // Process every binary variable that has
            // been fixed by Branch & Bound.
            foreach (KeyValuePair<int, int> fixedVariable
                     in node.FixedBinaryValues)
            {
                int variableIndex = fixedVariable.Key;

                int fixedValue =   fixedVariable.Value;

                // If the binary variable is fixed to 1,
                // its objective coefficient becomes a
                // constant contribution to Z.
                if (fixedValue == 1)
                {
                    objectiveOffset += preparedModel.ObjectiveCoefficients[variableIndex];
                }

                // Remove the fixed variable's contribution
                // from the objective because it is now constant.
                preparedModel
                    .ObjectiveCoefficients[variableIndex] = 0.0;

                // Substitute the fixed value into every constraint.
                foreach (Constraint constraint
                         in preparedModel.Constraints)
                {
                    double coefficient =
                        constraint.Coefficients[variableIndex];

                    // Move coefficient * fixedValue
                    // to the right-hand side.
                    constraint.RHS -=  coefficient * fixedValue;

                    // The variable is fixed, so it no longer
                    // participates in the LP relaxation.
                    constraint.Coefficients[variableIndex] = 0.0;
                }
            }

            // Remove branch constraints involving >=
            // because their effect is already represented
            // by FixedBinaryValues.
            preparedModel.Constraints =   preparedModel.Constraints .Where(c => c.Relation == "<=") .ToList();

            return preparedModel;
        }

        /// <summary>
        /// Adds the LP-relaxation upper bound x <= 1
        /// for every binary decision variable.
        ///
        /// Non-negativity is already assumed by the
        /// primal Simplex model, so a binary variable
        /// becomes 0 <= x <= 1 in the LP relaxation.
        /// </summary>
        private void AddBinaryUpperBounds(
            LinearModel model)
        {
            for (int i = 0;
                 i < model.SignRestrictions.Count;
                 i++)
            {
                string restriction =  model.SignRestrictions[i] .Trim().ToLower();

                if (restriction == "bin")
                {
                    Constraint upperBound = CreateBranchConstraint( model.ObjectiveCoefficients.Count,  i,  "<=",  1.0 );

                    model.Constraints.Add( upperBound  );
                }
            }
        }
        /// <summary>
        /// Creates a constraint for one Branch & Bound variable.
        ///
        /// Example:
        /// variableIndex = 1 means x2.
        ///
        /// x2 <= 2 becomes:
        /// 0x1 + 1x2 + 0x3 <= 2
        /// </summary>
        private Constraint CreateBranchConstraint(int numberOfVariables, int variableIndex, string relation,   double rhs)
        {
            Constraint branchConstraint =      new Constraint();

            for (int i = 0; i < numberOfVariables;  i++)
            {
                if (i == variableIndex)
                {
                    branchConstraint.Coefficients.Add(1.0);
                }
                else
                {
                    branchConstraint.Coefficients.Add(0.0);
                }
            }

            branchConstraint.Relation = relation;
            branchConstraint.RHS = rhs;

            return branchConstraint;
        }
        /// <summary>
        /// Checks whether the current BaseSimplex implementation
        /// can directly solve all constraints in this model.
        ///
        /// At the moment BaseSimplex assumes <= constraints
        /// with a positive slack variable.
        /// </summary>
        private bool CanBaseSimplexSolveDirectly( LinearModel model)
        {
            foreach (Constraint constraint in model.Constraints)
            {
                if (constraint.Relation != "<=")
                {
                    return false;
                }

                if (constraint.RHS < 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Recursively explores a Branch & Bound node.
        ///
        /// This is the core of the Branch & Bound algorithm.
        /// A node will either:
        /// - be fathomed,
        /// - become an integer candidate,
        /// - or branch into two new child nodes.
        /// </summary>
        private void ExploreNode(
    Node node,
    bool[] integerVariables)
        {
            LogLine();
            LogLine( $"Exploring Sub-Problem {node.SubProblemId}");
            LogLine(     $"Depth: {node.Depth}"    );

            if (!string.IsNullOrEmpty(node.BranchDescription))
            {
                LogLine(   $"Branch: {node.BranchDescription}"    );
            }

            // Solve the LP relaxation for this sub-problem
            // using Tobie's BaseSimplex solver.
            SolveRelaxation(node);

            LogLine(  $"Objective Value: {node.ObjectiveValue:0.###}"    );

            // Display the decision-variable values
            // returned by the Simplex solver.
            for (int i = 0; i < node.VariableValues.Length;  i++)
            {
                LogLine( $"x{i + 1} = {node.VariableValues[i]:0.###}"   );
            }
            bool isMaximization = node.Model.OptimizationType.Trim().ToLower() == "max";

            if (CannotBeatIncumbent( node, isMaximization))
            {
                FathomNode(  node,  "Bound cannot beat incumbent"  );

                return;
            }

            // Find the first variable that is required
            // to be integer but currently has a
            // fractional value.
            int variableIndex =SelectFractionalVariable(   node.VariableValues,integerVariables  );

            // If -1 is returned, every variable that
            // must be integer is already integer.
            if (variableIndex == -1)
            {
                // All integer-restricted variables are integer,
                // so this node is a feasible integer candidate.           

                // Compare this integer solution with the
                // current best solution found so far.
                UpdateIncumbent(  node, isMaximization );

                // An integer node does not need to branch again.
                FathomNode( node, "Integer solution" );

                return;
            }

            // Get the fractional value that we
            // are going to branch on.
            double fractionalValue =    node.VariableValues[variableIndex];

            LogLine(  $"Fractional variable found: " +  $"x{variableIndex + 1} = " +$"{fractionalValue:0.###}"
            );

            // Create the two child sub-problems.
            CreateBranches(  node,  variableIndex,fractionalValue);

            // Explore the left branch first.
            if (node.LeftChild != null)
            {
                ExploreNode(  node.LeftChild, integerVariables );
            }

            // After the left branch has been fully explored,
            // come back and explore the right branch.
            if (node.RightChild != null)
            {
                ExploreNode( node.RightChild,  integerVariables  );
            }
        }
        /// <summary>
        /// Solves the LP relaxation for a Branch & Bound sub-problem
        /// using the existing BaseSimplex solver.
        /// </summary>
        private SimplexResult SolveRelaxation(Node node)
        {
            // Safety check:
            // every Branch & Bound node must contain a LinearModel.
            if (node.Model == null)
            {
                throw new InvalidOperationException(
                    $"Sub-Problem {node.SubProblemId} does not contain a LinearModel."
                );
            }
            
            // Build a Simplex-compatible version of this node's model.
            // Any binary variables fixed by branching are substituted
            // into the model before it is sent to BaseSimplex.
            double objectiveOffset;

            LinearModel modelForSimplex = PrepareModelForSimplex( node,  out objectiveOffset  );

            // Safety check the prepared model, not the original node model.
            if (!CanBaseSimplexSolveDirectly(modelForSimplex))
            {
                throw new InvalidOperationException(
                    $"Sub-Problem {node.SubProblemId} contains a constraint " +
                    "that the current BaseSimplex implementation cannot solve directly."
                );
            }

            // Create a fresh solver for this sub-problem.
            BaseSimplex simplexSolver = new BaseSimplex();

            // Solve the prepared LP relaxation.
            simplexSolver.InitializeTableau( modelForSimplex);

            simplexSolver.Solve();

            // Add all of Tobie's tableau iterations for this
            // Branch & Bound sub-problem to our master log.
            IterationLog.Append(simplexSolver.IterationLog.ToString() );
            SimplexResult result =  simplexSolver.GetResult();

            // Store the Simplex result.
            node.SimplexResult = result;

            // Add back the objective contribution from
            // binary variables that were fixed to 1.
            node.ObjectiveValue = result.OptimalZ + objectiveOffset;

            // Convert Tobie's Dictionary<string, double>
            // into the double[] structure your current Branch & Bound
            // code already uses.
            node.VariableValues =new double[node.Model.ObjectiveCoefficients.Count];

            for (int i = 0;
                 i < node.Model.ObjectiveCoefficients.Count;
                 i++)
            {
                string variableName = $"x{i + 1}";

                if (result.VariableValues.ContainsKey(variableName))
                {
                    node.VariableValues[i] = result.VariableValues[variableName];
                }
                else
                {
                    node.VariableValues[i] = 0.0;
                }
            }
            // Restore binary variables that were fixed by branching.
            // They were substituted out of the LP calculation,
            // so put their fixed 0/1 values back into the node result.
            foreach (KeyValuePair<int, int> fixedVariable
                     in node.FixedBinaryValues)
            {
                node.VariableValues[fixedVariable.Key] = fixedVariable.Value;
            }

            node.IsSolved = true;
            node.IsFeasible = true;
            node.IsUnbounded = false;

            return result;
        }

        /// <summary>
        /// Marks a node as fathomed so that
        /// it will not be explored any further.
        /// </summary>
        private void FathomNode(   Node node,   string reason)
        {
            node.IsFathomed = true;
            node.FathomReason = reason;

            LogLine(  $"Sub-Problem {node.SubProblemId} fathomed: {reason}"     );
        }

        private void UpdateIncumbent(Node node, bool isMaximization)
        {
            // If we have no incumbent yet, the first feasible
            // integer solution automatically becomes the incumbent.
            bool isBetter = !hasIncumbent;

            if (hasIncumbent)
            {
                if (isMaximization)
                {
                    isBetter =
                        node.ObjectiveValue > incumbentObjectiveValue;
                }
                else
                {
                    isBetter =
                        node.ObjectiveValue < incumbentObjectiveValue;
                }
            }

            if (isBetter)
            {
                hasIncumbent = true;

                incumbentObjectiveValue = node.ObjectiveValue;

                incumbentVariableValues =  (double[])node.VariableValues.Clone();

                incumbentNodeId =   node.SubProblemId;

                LogLine($"NEW INCUMBENT: Sub-Problem {node.SubProblemId}"    );

                LogLine( $"Objective Value = {node.ObjectiveValue:0.000}"       );
            }
        }

        /// <summary>
        /// Checks whether a node's LP bound can still improve
        /// on the current incumbent.
        ///
        /// Returns true if the node should be fathomed by bound.
        /// </summary>
        private bool CannotBeatIncumbent(Node node,bool isMaximization)
        {
            // If we do not have an integer solution yet,
            // there is nothing to compare against.
            if (!hasIncumbent)
            {
                return false;
            }

            if (isMaximization)
            {
                // For MAX:
                // if the LP relaxation is already less than
                // or equal to the incumbent, this branch
                // cannot produce a better integer solution.
                return node.ObjectiveValue <= incumbentObjectiveValue + IntegerTolerance;
            }
            else
            {
                // For MIN:
                // if the LP relaxation is already greater than
                // or equal to the incumbent, this branch
                // cannot produce a better integer solution.
                return node.ObjectiveValue >= incumbentObjectiveValue - IntegerTolerance;
            }
        }
        /// <summary>
        /// Temporary recursive test for Branch & Bound.
        /// This simulates Simplex results at different nodes
        /// so that we can test repeated branching and backtracking.
        /// </summary>
    //    public void TestRecursiveBranching()
    //    {
    //        nodes.Clear();
    //        nodeCounter = 0;
    //        hasIncumbent = false;
    //        incumbentObjectiveValue = 0.0;
    //        incumbentVariableValues = new double[0];
    //        incumbentNodeId = "";
    //        Node rootNode = CreateNode("1", 0);
    //        rootNode.BranchDescription = "Root problem";

    //        bool[] integerVariables =
    //        {
    //    true,
    //    true
    //};

    //        // Start the recursive test.
    //        ExploreTestNode(
    //            rootNode,
    //            integerVariables
    //        );
    //        Console.WriteLine();
    //        Console.WriteLine("==============================");
    //        Console.WriteLine("BEST INTEGER SOLUTION");
    //        Console.WriteLine("==============================");

    //        if (hasIncumbent)
    //        {
    //            Console.WriteLine(
    //                $"Best Sub-Problem: {incumbentNodeId}"
    //            );

    //            Console.WriteLine(
    //                $"Objective Value: {incumbentObjectiveValue:0.000}"
    //            );

    //            for (int i = 0;
    //                 i < incumbentVariableValues.Length;
    //                 i++)
    //            {
    //                Console.WriteLine(
    //                    $"x{i + 1} = {incumbentVariableValues[i]:0.000}"
    //                );
    //            }
    //        }
    //        else
    //        {
    //            Console.WriteLine(
    //                "No feasible integer solution was found."
    //            );
    //        }
    //    }

    //    /// <summary>
    //    /// Simulates LP solutions for different nodes
    //    /// and recursively explores the Branch & Bound tree.
    //    /// </summary>
    //    private void ExploreTestNode(
    //        Node node,
    //        bool[] integerVariables)
    //    {
    //        double[] variableValues;

    //        // TEMPORARY TEST DATA
    //        // Later these values will come from Simplex.
    //        switch (node.SubProblemId)
    //        {
    //            case "1":
    //                variableValues = new double[]
    //                {
    //            4.000,
    //            2.750
    //                };
    //                break;

    //            case "1.1":
    //                variableValues = new double[]
    //                {
    //            4.600,
    //            2.000
    //                };
    //                break;

    //            case "1.2":
    //                variableValues = new double[]
    //                {
    //            5.000,
    //            3.000
    //                };
    //                break;

    //            case "1.1.1":
    //                variableValues = new double[]
    //                {
    //            4.000,
    //            2.000
    //                };
    //                break;

    //            case "1.1.2":
    //                variableValues = new double[]
    //                {
    //            5.000,
    //            2.000
    //                };
    //                break;

    //            default:
    //                variableValues = new double[]
    //                {
    //            0.000,
    //            0.000
    //                };
    //                break;
    //        }

    //        Console.WriteLine();
    //        Console.WriteLine("==============================");
    //        Console.WriteLine($"Exploring Sub-Problem {node.SubProblemId}");
    //        Console.WriteLine($"Depth: {node.Depth}");
    //        Console.WriteLine($"Branch: {node.BranchDescription}");

    //        Console.WriteLine(
    //            $"x1 = {variableValues[0]:0.000}"
    //        );

    //        Console.WriteLine(
    //            $"x2 = {variableValues[1]:0.000}"
    //        );

    //        // TEMPORARY objective function:
    //        // MAX Z = 5x1 + 4x2
    //        //
    //        // Later these coefficients will come
    //        // from the actual LinearModel.
    //        node.ObjectiveValue =
    //            (5 * variableValues[0]) +
    //            (4 * variableValues[1]);

    //        Console.WriteLine(
    //            $"Objective Value: {node.ObjectiveValue:0.000}"
    //        );

    //        // Check whether this node can still improve
    //        // on the current best integer solution.
    //        if (CannotBeatIncumbent(node, true))
    //        {
    //            FathomNode(
    //                node,
    //                "Bound cannot beat incumbent"
    //            );

    //            return;
    //        }
    //        int variableIndex =
    //            SelectFractionalVariable(
    //                variableValues,
    //                integerVariables
    //            );

    //        // No fractional integer variables remain.
    //        if (variableIndex == -1)
    //        {
    //            // We found a feasible integer candidate.
    //            UpdateIncumbent(
    //                node,
    //                true
    //            );

    //            FathomNode(
    //                node,
    //                "Integer solution"
    //            );

    //            return;
    //        }

    //        double fractionalValue =
    //            variableValues[variableIndex];

    //        Console.WriteLine(
    //            $"Fractional variable: " +
    //            $"x{variableIndex + 1} = {fractionalValue:0.000}"
    //        );

    //        // Create left and right children.
    //        CreateBranches(
    //            node,
    //            variableIndex,
    //            fractionalValue
    //        );

    //        // RECURSION / BACKTRACKING
    //        ExploreTestNode(
    //            node.LeftChild,
    //            integerVariables
    //        );

    //        ExploreTestNode(
    //            node.RightChild,
    //            integerVariables
    //        );
    //    }
    //    /// <summary>
    //    /// Temporary test method used to prove that
    //    /// Branch & Bound node creation is working correctly.
    //    /// </summary>
    //    public void TestBranchCreation()
    //    {
    //        // Reset everything first.
    //        nodes.Clear();
    //        nodeCounter = 0;

    //        // Create the root node.
    //        Node rootNode = CreateNode("1", 0);

    //        rootNode.BranchDescription = "Root problem";

    //        // Pretend that Simplex gave us:
    //        //
    //        // x1 = 4.000
    //        // x2 = 2.750
    //        // x3 = 1.000
    //        //
    //        // and all three variables must be integers.
    //        double[] variableValues =
    //        {
    //    4.000,
    //    2.750,
    //    1.000
    //};

    //        bool[] integerVariables =
    //        {
    //    true,
    //    true,
    //    true
    //};

    //        // Find the first fractional integer variable.
    //        int variableIndex =
    //            SelectFractionalVariable(
    //                variableValues,
    //                integerVariables
    //            );

    //        if (variableIndex == -1)
    //        {
    //            Console.WriteLine(
    //                "All required variables are already integer."
    //            );

    //            return;
    //        }

    //        double fractionalValue =
    //            variableValues[variableIndex];

    //        Console.WriteLine("BRANCH & BOUND TEST");
    //        Console.WriteLine("-----------------------------");

    //        Console.WriteLine(
    //            $"Fractional variable found: " +
    //            $"x{variableIndex + 1} = {fractionalValue:0.000}"
    //        );

    //        // Create the left and right child nodes.
    //        CreateBranches(
    //            rootNode,
    //            variableIndex,
    //            fractionalValue
    //        );

    //        Console.WriteLine();
    //        Console.WriteLine($"Root Node: {rootNode.SubProblemId}");

    //        Console.WriteLine(
    //            $"Left Child: Node {rootNode.LeftChild.SubProblemId}"
    //        );

    //        Console.WriteLine(
    //            $"Branch: {rootNode.LeftChild.BranchDescription}"
    //        );

    //        Console.WriteLine();

    //        Console.WriteLine(
    //            $"Right Child: Node {rootNode.RightChild.SubProblemId}"
    //        );

    //        Console.WriteLine(
    //            $"Branch: {rootNode.RightChild.BranchDescription}"
    //        );
    //    }
    //    /// <summary>
    //    /// Temporary test that proves a child node can branch again,
    //    /// creating a multi-level Branch & Bound tree.
    //    /// </summary>
    //    public void TestMultiLevelBranching()
    //    {
    //        nodes.Clear();
    //        nodeCounter = 0;

    //        // Create root node.
    //        Node rootNode = CreateNode("1", 0);
    //        rootNode.BranchDescription = "Root problem";

    //        // Pretend the root LP solution contains:
    //        //
    //        // x1 = 4.000
    //        // x2 = 2.750
    //        //
    //        // x2 is fractional, so branch on x2.
    //        double[] rootValues =
    //        {
    //    4.000,
    //    2.750
    //};

    //        bool[] integerVariables =
    //        {
    //    true,
    //    true
    //};

    //        int firstVariable =
    //            SelectFractionalVariable(
    //                rootValues,
    //                integerVariables
    //            );

    //        double firstFractionalValue =
    //            rootValues[firstVariable];

    //        CreateBranches(
    //            rootNode,
    //            firstVariable,
    //            firstFractionalValue
    //        );

    //        Console.WriteLine();
    //        Console.WriteLine("LEVEL 1");
    //        Console.WriteLine("-----------------------------");

    //        Console.WriteLine(
    //            $"Node {rootNode.SubProblemId}: Root"
    //        );

    //        Console.WriteLine(
    //            $"Node {rootNode.LeftChild.SubProblemId}: " +
    //            $"{rootNode.LeftChild.BranchDescription}"
    //        );

    //        Console.WriteLine(
    //            $"Node {rootNode.RightChild.SubProblemId}: " +
    //            $"{rootNode.RightChild.BranchDescription}"
    //        );

    //        // Now pretend Node 2 was solved using Simplex
    //        // and still contains a fractional integer value:
    //        //
    //        // x1 = 4.600
    //        // x2 = 2.000
    //        //
    //        // Therefore Node 2 must branch again on x1.
    //        double[] node2Values =
    //        {
    //    4.600,
    //    2.000
    //};

    //        int secondVariable =
    //            SelectFractionalVariable(
    //                node2Values,
    //                integerVariables
    //            );

    //        if (secondVariable != -1)
    //        {
    //            double secondFractionalValue =
    //                node2Values[secondVariable];

    //            CreateBranches(
    //                rootNode.LeftChild,
    //                secondVariable,
    //                secondFractionalValue
    //            );
    //        }

    //        Console.WriteLine();
    //        Console.WriteLine("LEVEL 2");
    //        Console.WriteLine("-----------------------------");

    //        Console.WriteLine(
    //            $"Node {rootNode.LeftChild.LeftChild.SubProblemId}: " +
    //            $"{rootNode.LeftChild.LeftChild.BranchDescription}"
    //        );

    //        Console.WriteLine(
    //            $"Node {rootNode.LeftChild.RightChild.SubProblemId}: " +
    //            $"{rootNode.LeftChild.RightChild.BranchDescription}"
    //        );
    //    }
    }
}
