using LPR381.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LPR381.Layer2_SolverEngine
{
    // -------------------------------------------------------------------------
    // CLASS: BranchAndBound
    // Purpose: This handles variables that MUST be whole numbers (integers).
    // If standard Simplex says we should produce "2.5 chairs", this algorithm splits 
    // the problem into two parallel universes (one where we make <= 2 chairs, 
    // and one where we make >= 3 chairs) and tests both to find the best integer reality!
    // -------------------------------------------------------------------------
    internal class BranchAndBound
    {
        // Keeps track of every "what-if" parallel universe (Node) we create.
        private List<Node> nodes;

        // Gives each node a unique ID number so we can track them.
        private int nodeCounter;

        // 'Incumbent' is just a fancy word for "The best valid answer we've found so far".
        // This tracks if we actually have a valid integer answer yet.
        private bool hasIncumbent;

        // Stores the profit/cost of the best answer found so far.
        private double incumbentObjectiveValue;

        // Stores the actual variable answers (x1, x2, etc.) for that best answer.
        private double[] incumbentVariableValues;

        // Remembers exactly which node gave us the winning answer.
        private string incumbentNodeId;

        // Computers sometimes struggle with exact decimals (like 3.000000001).
        // This tiny tolerance helps the computer realize it's basically just '3'.
        private const double IntegerTolerance = 0.000001;

        // We use this to write down everything we do so the Exporter can save it to a file later.
        public StringBuilder IterationLog { get; private set; } = new StringBuilder();

        // Sets up a completely fresh slate when we start a new problem.
        public BranchAndBound()
        {
            nodes = new List<Node>();
            nodeCounter = 0;
            hasIncumbent = false;
            incumbentObjectiveValue = 0.0;
            incumbentVariableValues = new double[0];
            incumbentNodeId = "";
        }

        // -------------------------------------------------------------------------
        // METHOD: Solve
        // Purpose: The main engine starter. It takes the original problem, sets up 
        // the very first "Root" node, and kicks off the tree exploration.
        // -------------------------------------------------------------------------
        public SimplexResult Solve(LinearModel model, bool[] integerVariables)
        {
            // Wipe the memory clean so we don't accidentally mix up old problems.
            nodes.Clear();
            nodeCounter = 0;
            hasIncumbent = false;
            incumbentObjectiveValue = 0.0;
            incumbentVariableValues = new double[0];
            incumbentNodeId = null;
            IterationLog.Clear();

            // Create Node 1: The original problem exactly as written.
            Node rootNode = CreateNode("1", 0);
            rootNode.Model = CloneModel(model);

            // If a variable is "binary" (0 or 1), we tell the math it can't go above 1.
            AddBinaryUpperBounds(rootNode.Model);

            rootNode.BranchDescription = "Root problem";

            LogLine();
            LogLine("==============================");
            LogLine("        BRANCH & BOUND");
            LogLine("==============================");

            // Dive into the tree and start exploring!
            ExploreNode(rootNode, integerVariables);

            LogLine();
            LogLine("==================================");
            LogLine("      FINAL INTEGER SOLUTION");
            LogLine("==================================");

            // Print the final, absolute best integer answer we found.
            if (hasIncumbent)
            {
                LogLine($"Best Sub-Problem: {incumbentNodeId}");
                LogLine($"Optimal Objective Value: {incumbentObjectiveValue:0.###}");
                LogLine();
                LogLine("Decision Variables:");

                for (int i = 0; i < incumbentVariableValues.Length; i++)
                {
                    LogLine($"x{i + 1} = {incumbentVariableValues[i]:0.###}");
                }
            }
            else
            {
                LogLine("No feasible integer solution was found.");
            }

            LogLine("==================================");

            // We package our final integer answer into the exact same "SimplexResult" 
            // format that standard Simplex uses, so the Exporter handles it perfectly!
            SimplexResult finalResult = new SimplexResult();

            if (hasIncumbent)
            {
                finalResult.OptimalZ = incumbentObjectiveValue;
                for (int i = 0; i < incumbentVariableValues.Length; i++)
                {
                    finalResult.VariableValues[$"x{i + 1}"] = incumbentVariableValues[i];
                }
            }

            return finalResult;
        }

        // Helper: Prints text to the screen AND saves it to our text file log simultaneously.
        private void LogLine(string message = "")
        {
            Console.WriteLine(message);
            IterationLog.AppendLine(message);
        }

        // Helper: Creates a new sub-problem (node) and tracks its depth in the tree.
        private Node CreateNode(string subProblemId, int depth)
        {
            Node newNode = new Node(subProblemId, depth);
            nodes.Add(newNode);
            return newNode;
        }

        // Helper: Checks if a decimal like 4.0000001 is safely considered a whole number.
        private bool IsInteger(double value)
        {
            return Math.Abs(value - Math.Round(value)) <= IntegerTolerance;
        }

        // -------------------------------------------------------------------------
        // METHOD: SelectFractionalVariable
        // Purpose: Scans our variables to find the first one that is SUPPOSED to be 
        // a whole number, but came out as a decimal (e.g., x2 = 2.5).
        // -------------------------------------------------------------------------
        private int SelectFractionalVariable(double[] variableValues, bool[] integerVariables)
        {
            for (int i = 0; i < variableValues.Length; i++)
            {
                // Only inspect variables that are specifically marked as 'int' or 'bin'.
                if (integerVariables[i])
                {
                    if (!IsInteger(variableValues[i]))
                    {
                        return i; // Found the culprit!
                    }
                }
            }
            return -1; // -1 means everything is perfectly whole numbers!
        }

        // Helper: Takes a decimal like 2.75 and returns 2 (lower) and 3 (upper)
        private void GetBranchValues(double fractionalValue, out double lowerBranchValue, out double upperBranchValue)
        {
            lowerBranchValue = Math.Floor(fractionalValue);
            upperBranchValue = Math.Ceiling(fractionalValue);
        }

        // -------------------------------------------------------------------------
        // METHOD: CreateBranches
        // Purpose: Splits the universe into two new child nodes. 
        // Example: If x2 = 2.75, Left Child gets a new rule: x2 <= 2. 
        // Right child gets a new rule: x2 >= 3.
        // -------------------------------------------------------------------------
        private void CreateBranches(Node parentNode, int variableIndex, double fractionalValue)
        {
            GetBranchValues(fractionalValue, out double lowerBranchValue, out double upperBranchValue);

            string variableRestriction = parentNode.Model.SignRestrictions[variableIndex].Trim().ToLower();
            bool isBinary = variableRestriction == "bin";

            // If it's a Yes/No (Binary) variable, the branches are strictly 0 and 1.
            if (isBinary)
            {
                lowerBranchValue = 0;
                upperBranchValue = 1;
            }

            // --- LEFT CHILD CREATION (x <= lower value) ---
            Node leftChild = CreateNode(parentNode.SubProblemId + ".1", parentNode.Depth + 1);
            leftChild.Parent = parentNode;
            leftChild.BranchDescription = $"x{variableIndex + 1} <= {lowerBranchValue:0.###}";

            // Give the child a clean copy of the parent's math model
            leftChild.Model = CloneModel(parentNode.Model);
            leftChild.FixedBinaryValues = new Dictionary<int, int>(parentNode.FixedBinaryValues);

            if (isBinary) leftChild.FixedBinaryValues[variableIndex] = 0;

            // Inject the new physical constraint into the math model
            Constraint leftConstraint = CreateBranchConstraint(leftChild.Model.ObjectiveCoefficients.Count, variableIndex, "<=", lowerBranchValue);
            leftChild.Model.Constraints.Add(leftConstraint);


            // --- RIGHT CHILD CREATION (x >= upper value) ---
            Node rightChild = CreateNode(parentNode.SubProblemId + ".2", parentNode.Depth + 1);
            rightChild.Parent = parentNode;
            rightChild.BranchDescription = $"x{variableIndex + 1} >= {upperBranchValue:0.###}";

            rightChild.Model = CloneModel(parentNode.Model);
            rightChild.FixedBinaryValues = new Dictionary<int, int>(parentNode.FixedBinaryValues);

            if (isBinary) rightChild.FixedBinaryValues[variableIndex] = 1;

            Constraint rightConstraint = CreateBranchConstraint(rightChild.Model.ObjectiveCoefficients.Count, variableIndex, ">=", upperBranchValue);
            rightChild.Model.Constraints.Add(rightConstraint);


            // Link the family tree together
            parentNode.LeftChild = leftChild;
            parentNode.RightChild = rightChild;
        }

        // Helper: Creates a deep copy of the LinearModel so siblings don't share and corrupt the same memory.
        private LinearModel CloneModel(LinearModel originalModel)
        {
            LinearModel copy = new LinearModel();
            copy.OptimizationType = originalModel.OptimizationType;
            copy.ObjectiveCoefficients = new List<double>(originalModel.ObjectiveCoefficients);
            copy.SignRestrictions = new List<string>(originalModel.SignRestrictions);

            foreach (Constraint originalConstraint in originalModel.Constraints)
            {
                Constraint constraintCopy = new Constraint();
                constraintCopy.Coefficients = new List<double>(originalConstraint.Coefficients);
                constraintCopy.Relation = originalConstraint.Relation;
                constraintCopy.RHS = originalConstraint.RHS;
                copy.Constraints.Add(constraintCopy);
            }

            return copy;
        }

        // -------------------------------------------------------------------------
        // METHOD: PrepareModelForSimplex
        // Purpose: Prevents standard Simplex from crashing when variables are permanently stuck to a value (like x1 = 1).
        // It does the math ahead of time, deducting it from the capacity and adjusting constraints.
        // -------------------------------------------------------------------------
        private LinearModel PrepareModelForSimplex(Node node, out double objectiveOffset)
        {
            LinearModel preparedModel = CloneModel(node.Model);
            objectiveOffset = 0.0;

            foreach (KeyValuePair<int, int> fixedVariable in node.FixedBinaryValues)
            {
                int variableIndex = fixedVariable.Key;
                int fixedValue = fixedVariable.Value;

                // If a binary variable is locked to 1, we immediately pocket its profit.
                if (fixedValue == 1)
                {
                    objectiveOffset += preparedModel.ObjectiveCoefficients[variableIndex];
                }

                // Remove the variable from the active math search.
                preparedModel.ObjectiveCoefficients[variableIndex] = 0.0;

                foreach (Constraint constraint in preparedModel.Constraints)
                {
                    double coefficient = constraint.Coefficients[variableIndex];

                    // Subtract the required capacity from the RHS rule.
                    constraint.RHS -= coefficient * fixedValue;
                    constraint.Coefficients[variableIndex] = 0.0;
                }
            }

            // --- CRITICAL FIX FOR NEGATIVE RHS ---
            // If subtracting the fixed binary value dropped a constraint's limit below zero, 
            // the standard Simplex engine will fail. We mathematically fix this by multiplying 
            // the entire row by -1 and flipping the sign (e.g. -x <= -5 becomes x >= 5).
            foreach (Constraint constraint in preparedModel.Constraints)
            {
                if (constraint.RHS < 0)
                {
                    constraint.RHS *= -1;
                    for (int i = 0; i < constraint.Coefficients.Count; i++)
                    {
                        constraint.Coefficients[i] *= -1;
                    }

                    // Flip the operator
                    if (constraint.Relation == "<=") constraint.Relation = ">=";
                    else if (constraint.Relation == ">=") constraint.Relation = "<=";
                    // "=" remains "="
                }
            }

            return preparedModel;
        }

        // Helper: Ensures binary variables don't accidentally jump to 2 or 3 in the standard solver.
        private void AddBinaryUpperBounds(LinearModel model)
        {
            for (int i = 0; i < model.SignRestrictions.Count; i++)
            {
                string restriction = model.SignRestrictions[i].Trim().ToLower();
                if (restriction == "bin")
                {
                    Constraint upperBound = CreateBranchConstraint(model.ObjectiveCoefficients.Count, i, "<=", 1.0);
                    model.Constraints.Add(upperBound);
                }
            }
        }

        // Helper: Builds a physical math constraint for the child nodes (e.g., x2 <= 2)
        private Constraint CreateBranchConstraint(int numberOfVariables, int variableIndex, string relation, double rhs)
        {
            Constraint branchConstraint = new Constraint();
            for (int i = 0; i < numberOfVariables; i++)
            {
                // Put a '1' in the slot of the specific variable we are restricting, and '0' everywhere else.
                if (i == variableIndex) branchConstraint.Coefficients.Add(1.0);
                else branchConstraint.Coefficients.Add(0.0);
            }
            branchConstraint.Relation = relation;
            branchConstraint.RHS = rhs;
            return branchConstraint;
        }

        // Helper: Verifies the constraints are safe for our BaseSimplex Engine to solve.
        private bool CanBaseSimplexSolveDirectly(LinearModel model)
        {
            foreach (Constraint constraint in model.Constraints)
            {
                // Our upgraded BaseSimplex with Big-M natively supports all three of these!
                if (constraint.Relation != "<=" && constraint.Relation != ">=" && constraint.Relation != "=") return false;

                // RHS should never be negative thanks to our flip logic in PrepareModelForSimplex.
                if (constraint.RHS < 0) return false;
            }
            return true;
        }

        // -------------------------------------------------------------------------
        // METHOD: ExploreNode
        // Purpose: The true heart of Branch and Bound. It looks at a node, solves its math,
        // and decides whether to kill it (fathom), keep it (incumbent), or split it into children.
        // -------------------------------------------------------------------------
        private void ExploreNode(Node node, bool[] integerVariables)
        {
            LogLine();
            LogLine($"Exploring Sub-Problem {node.SubProblemId}");
            LogLine($"Depth: {node.Depth}");

            if (!string.IsNullOrEmpty(node.BranchDescription))
            {
                LogLine($"Branch: {node.BranchDescription}");
            }

            // Call upon our Base Simplex engine to solve the pure math of this specific node.
            SolveRelaxation(node);

            // If the math proved this branch is impossible (or unbounded), kill it and stop exploring here.
            // This prevents the entire program from crashing!
            if (!node.IsFeasible || node.IsUnbounded)
            {
                string failReason = node.IsUnbounded ? "Unbounded solution" : "Infeasible mathematically impossible sub-problem";
                FathomNode(node, failReason);
                return;
            }

            LogLine($"Objective Value: {node.ObjectiveValue:0.###}");

            // Show what answers Simplex gave us.
            for (int i = 0; i < node.VariableValues.Length; i++)
            {
                LogLine($"x{i + 1} = {node.VariableValues[i]:0.###}");
            }

            bool isMaximization = node.Model.OptimizationType.Trim().ToLower() == "max";

            // If this node's absolute best potential profit is STILL lower than an integer answer 
            // we already found earlier, we kill the node entirely. No reason to keep searching it!
            if (CannotBeatIncumbent(node, isMaximization))
            {
                FathomNode(node, "Bound cannot beat incumbent.");
                return;
            }

            // Check if any numbers came out as decimals when they shouldn't have.
            int variableIndex = SelectFractionalVariable(node.VariableValues, integerVariables);

            // If ALL numbers are perfectly whole integers!
            if (variableIndex == -1)
            {
                // Save this as our new "best" answer (Incumbent)
                UpdateIncumbent(node, isMaximization);

                // We reached the bottom of this specific branch successfully.
                FathomNode(node, "Integer solution found.");
                return;
            }

            // If we DO have a decimal, we must split the universe again.
            double fractionalValue = node.VariableValues[variableIndex];
            LogLine($"Fractional variable found: x{variableIndex + 1} = {fractionalValue:0.###}");

            // Make the left and right children
            CreateBranches(node, variableIndex, fractionalValue);

            // Dive down the left child's path first
            if (node.LeftChild != null)
            {
                ExploreNode(node.LeftChild, integerVariables);
            }

            // Once the entire left side of the tree is finished, backtrack and check the right child!
            if (node.RightChild != null)
            {
                ExploreNode(node.RightChild, integerVariables);
            }
        }

        // -------------------------------------------------------------------------
        // METHOD: SolveRelaxation
        // Purpose: Assembles the model, hands it to BaseSimplex, translates the answers back,
        // and crucially, catches math exceptions so the tree doesn't blow up.
        // -------------------------------------------------------------------------
        private SimplexResult SolveRelaxation(Node node)
        {
            if (node.Model == null) throw new InvalidOperationException($"Sub-Problem {node.SubProblemId} does not contain a LinearModel.");

            double objectiveOffset;
            LinearModel modelForSimplex = PrepareModelForSimplex(node, out objectiveOffset);

            if (!CanBaseSimplexSolveDirectly(modelForSimplex))
            {
                throw new InvalidOperationException($"Sub-Problem {node.SubProblemId} contains a constraint that the current BaseSimplex implementation cannot solve directly.");
            }

            // Fire up the math engine!
            BaseSimplex simplexSolver = new BaseSimplex();
            simplexSolver.InitializeTableau(modelForSimplex);

            try
            {
                simplexSolver.Solve();
            }
            catch (InvalidOperationException ex)
            {
                // THE FIX: If BaseSimplex throws an error because the problem is impossible or unbounded,
                // we gracefully catch it, add the failed math to our log, and flag the node as dead.
                IterationLog.Append(simplexSolver.IterationLog.ToString());
                LogLine($"  [SYSTEM DETECTED]: {ex.Message}");
                node.IsSolved = true;
                node.IsFeasible = false;
                node.IsUnbounded = ex.Message.Contains("UNBOUNDED");
                return null;
            }

            // Collect the massive wall of text that Simplex generated and add it to our B&B log.
            IterationLog.Append(simplexSolver.IterationLog.ToString());
            SimplexResult result = simplexSolver.GetResult();

            node.SimplexResult = result;

            // Re-add any profit we pocketed from fixed binary variables earlier.
            node.ObjectiveValue = result.OptimalZ + objectiveOffset;
            node.VariableValues = new double[node.Model.ObjectiveCoefficients.Count];

            for (int i = 0; i < node.Model.ObjectiveCoefficients.Count; i++)
            {
                string variableName = $"x{i + 1}";
                if (result.VariableValues.ContainsKey(variableName)) node.VariableValues[i] = result.VariableValues[variableName];
                else node.VariableValues[i] = 0.0;
            }

            // Put the locked binary variables back into our final answer list.
            foreach (KeyValuePair<int, int> fixedVariable in node.FixedBinaryValues)
            {
                node.VariableValues[fixedVariable.Key] = fixedVariable.Value;
            }

            node.IsSolved = true;
            node.IsFeasible = true;
            node.IsUnbounded = false;

            return result;
        }

        // Helper: "Fathoming" just means we cross this node out and stop exploring it.
        private void FathomNode(Node node, string reason)
        {
            node.IsFathomed = true;
            node.FathomReason = reason;
            LogLine($"Sub-Problem {node.SubProblemId} fathomed: {reason}");
        }

        // Helper: Checks if the new node is officially the best answer we've found in the entire tree.
        private void UpdateIncumbent(Node node, bool isMaximization)
        {
            bool isBetter = !hasIncumbent;

            if (hasIncumbent)
            {
                if (isMaximization) isBetter = node.ObjectiveValue > incumbentObjectiveValue;
                else isBetter = node.ObjectiveValue < incumbentObjectiveValue;
            }

            // If it's better, overwrite our old favorite answer with this new one!
            if (isBetter)
            {
                hasIncumbent = true;
                incumbentObjectiveValue = node.ObjectiveValue;
                incumbentVariableValues = (double[])node.VariableValues.Clone();
                incumbentNodeId = node.SubProblemId;

                LogLine($"NEW INCUMBENT: Sub-Problem {node.SubProblemId}");
                LogLine($"Objective Value = {node.ObjectiveValue:0.000}");
            }
        }

        // Helper: The logic that decides if we should just abandon a sub-problem because it's too weak.
        private bool CannotBeatIncumbent(Node node, bool isMaximization)
        {
            if (!hasIncumbent) return false; // If we don't have a best answer yet, keep searching!

            if (isMaximization)
            {
                // If we want MAXIMUM profit, but this node's absolute ceiling is already lower than our best answer... abandon ship.
                return node.ObjectiveValue <= incumbentObjectiveValue + IntegerTolerance;
            }
            else
            {
                // If we want MINIMUM cost, but this node's absolute floor is already more expensive than our best... abandon ship.
                return node.ObjectiveValue >= incumbentObjectiveValue - IntegerTolerance;
            }
        }
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
