using System;
using System.Collections.Generic;

// 1. Core Interfaces
public interface IReteNode
{
    void Assert(object fact);
    void Retract(object fact); // New: Remove a fact from the network
}
// 2. Alpha Memory: Acts as a successor to Alpha Nodes
public class AlphaMemory : IReteNode
{
    public List<object> Facts { get; } = new();
    private readonly List<JoinNode> _successors = new();

    public void AddSuccessor(JoinNode node) => _successors.Add(node);

    public void Assert(object fact)
    {
        Facts.Add(fact);
        foreach (var join in _successors) join.RightAssert(fact);
    }

    public void Retract(object fact)
    {
        if (Facts.Remove(fact))
        {
            // Tell successors this fact is gone
            foreach (var join in _successors) join.RightRetract(fact);
        }
    }
}

// Represents a partial match or "tuple" of facts in the network
public class Token
{
    public List<object> Facts { get; } = new();
    public Token(object fact) => Facts.Add(fact);
    public Token(Token parent, object fact)
    {
        Facts.AddRange(parent.Facts);
        Facts.Add(fact);
    }
}

// 3. Updated AlphaConditionNode: Now pushes results to a memory
public class AlphaConditionNode<T> : IReteNode
{
    private readonly Func<T, bool> _predicate;
    private readonly IReteNode _successor; // Usually an AlphaMemory

    public AlphaConditionNode(Func<T, bool> predicate, IReteNode successor)
    {
        _predicate = predicate;
        _successor = successor;
    }

    public void Assert(object fact)
    {
        if (fact is T typedFact && _predicate(typedFact))
        {
            _successor.Assert(typedFact);
        }
    }

    public void Retract(object fact)
    {
        if (fact is T typedFact && _predicate(typedFact))
        {
            _successor.Retract(typedFact);
        }
    }
}

// 4. Beta Node (JoinNode): Now strictly handles the relationship logic
public class JoinNode
{
    private readonly AlphaMemory _leftInput;
    private readonly AlphaMemory _rightInput;
    private readonly Func<object, object, bool> _joinCondition;
    private readonly List<IReteNode> _successors = new();

    public JoinNode(AlphaMemory left, AlphaMemory right, Func<object, object, bool> condition)
    {
        _leftInput = left;
        _rightInput = right;
        _joinCondition = condition;
        left.AddSuccessor(this);
        right.AddSuccessor(this);
    }

    public void AddSuccessor(IReteNode node) => _successors.Add(node);

    public void RightAssert(object newRightFact)
    {
        foreach (var leftFact in _leftInput.Facts)
        {
            // Ensure we aren't comparing the exact same instance
            if (!ReferenceEquals(leftFact, newRightFact) && _joinCondition(leftFact, newRightFact))
            {
                // Create a Token representing the match and push to Terminal Node
                var matchToken = new Token(new List<object> { leftFact, newRightFact });
                foreach (var succ in _successors) succ.Assert(matchToken);
            }
        }
    }

    public void RightRetract(object fact)
    {
        // Any match involving this specific fact must be retracted
        foreach (var succ in _successors) succ.Retract(fact);
    }

}

// The Beta Node joins a Token (left) with a Fact (right)
/*
public class JoinNode : IReteNode
{
    private readonly List<Token> _leftMemory = new();  // Matches from "higher" in the tree
    private readonly List<object> _rightMemory = new(); // Facts from an Alpha Node
    private readonly Func<Token, object, bool> _joinCondition;
    private readonly List<Action<Token>> _actions = new();

    public JoinNode(Func<Token, object, bool> condition) => _joinCondition = condition;

    public void AddAction(Action<Token> action) => _actions.Add(action);

    // Called from the Left (Partial matches)
    public void LeftAssert(Token token)
    {
        _leftMemory.Add(token);
        foreach (var fact in _rightMemory)
        {
            Evaluate(token, fact);
        }
    }

    // Called from the Right (Alpha Memory / Single facts)
    public void Assert(object fact)
    {
        _rightMemory.Add(fact);
        foreach (var token in _leftMemory)
        {
            Evaluate(token, fact);
        }
    }

    private void Evaluate(Token token, object fact)
    {
        if (_joinCondition(token, fact))
        {
            var newToken = new Token(token, fact);
            Console.WriteLine("Beta Match: Join successful!");
            foreach (var action in _actions) action(newToken);
        }
    }
}
*/

public class TerminalNode : IReteNode
{
    private readonly string _ruleName;
    private readonly Action<Token> _action;
    private readonly Agenda _agenda;
    private readonly int _salience;

    public TerminalNode(string name, Action<Token> action, Agenda agenda, int salience = 0)
    {
        _ruleName = name;
        _action = action;
        _agenda = agenda;
        _salience = salience;
    }

    public void Assert(object fact)
    {
        if (fact is Token finalMatch)
        {
            _agenda.Add(new Activation(_ruleName, _action, finalMatch, _salience));
        }
    }

    public void Retract(object fact)
    {
        // Find and remove any activations in the agenda that contain this fact
        _agenda.RemoveByFact(fact);
        Console.WriteLine($"[RETRACT] Pending activation for rule '{_ruleName}' cancelled.");
    }
}


public class Activation
{
    public string RuleName { get; }
    public Action<Token> Action { get; }
    public Token Match { get; }
    public int Salience { get; } // Higher fires first

    public Activation(string name, Action<Token> action, Token match, int salience)
    {
        RuleName = name;
        Action = action;
        Match = match;
        Salience = salience;
    }

    public void Fire() => Action(Match);
}

public class Agenda
{
    private readonly List<Activation> _activations = new();

    public void Add(Activation a) => _activations.Add(a);

    public void RemoveByFact(object fact)
    {
        // Remove any activation where the Match (Token) contains the retracted fact
        _activations.RemoveAll(a => a.Match.Facts.Contains(fact));
    }

    public void FireAll()
    {
        var sorted = _activations.OrderByDescending(a => a.Salience).ToList();
        _activations.Clear();
        foreach (var activation in sorted) activation.Fire();
    }
}




// 5. Engine Implementation
public class ReteEngine
{
    private readonly List<IReteNode> _rootChildren = new();

    public void AddAlphaNode(IReteNode node) => _rootChildren.Add(node);

    public void Assert(object fact)
    {
        foreach (var child in _rootChildren) child.Assert(fact);
    }
}

// Usage Example
/*
public class Program
{
    public static void Main()
    {
        var engine = new ReteEngine();
        var cellMemory = new AlphaMemory();

        // Rule: "If two cells have the same ID but different values"
        var alphaFilter = new AlphaConditionNode<Cell>(c => c.Value > 0, cellMemory);
        var conflictJoin = new JoinNode(cellMemory, cellMemory, (a, b) => {
            var c1 = (Cell)a; var c2 = (Cell)b;
            return c1.Id == c2.Id && c1.Value != c2.Value;
        });

        engine.AddAlphaNode(alphaFilter);

        Console.WriteLine("Asserting Cell 1...");
        engine.Assert(new Cell { Id = "A1", Value = 100 }); // No conflict yet, just stored

        Console.WriteLine("Asserting Cell 2...");
        engine.Assert(new Cell { Id = "A1", Value = 200 }); // JoinNode triggers conflict!
    }
}
*/

/*
public class Program
{
    public static void Main()
    {
        // Setup infrastructure
        var engine = new ReteEngine();
        var agenda = new Agenda();
        var cellMemory = new AlphaMemory();

        // --- LINKING JOIN AND TERMINAL ---

        // Define the logic: "If two cells have same ID but different Values"
        var conflictJoin = new JoinNode(cellMemory, cellMemory, (a, b) => {
            var c1 = (Cell)a; var c2 = (Cell)b;
            return c1.Id == c2.Id && c1.Value != c2.Value;
        });

        // Define the Terminal Action
        Action<Token> ruleAction = (token) => {
            var c1 = (Cell)token.Facts[0];
            var c2 = (Cell)token.Facts[1];
            Console.WriteLine($"[RULE FIRED] Conflict on ID '{c1.Id}': {c1.Value} != {c2.Value}");
        };

        // Create Terminal Node and link it as a successor to the Join Node
        var terminalNode = new TerminalNode("ConflictDetector", ruleAction, agenda);
        conflictJoin.AddSuccessor(terminalNode);

        // Link Alpha Filter to the Engine
        var alphaFilter = new AlphaConditionNode<Cell>(c => c.Value > 0, cellMemory);
        engine.AddAlphaNode(alphaFilter);

        // --- 3. ASSERTING FACTS ---
        Console.WriteLine("Asserting Cell A1 (Val: 100)...");
        engine.Assert(new Cell { Id = "A1", Value = 100 });

        Console.WriteLine("Asserting Cell A1 (Val: 200)...");
        engine.Assert(new Cell { Id = "A1", Value = 200 });

        // --- 4. FIRING THE AGENDA ---
        Console.WriteLine("\nChecking Agenda for pending activations...");
        agenda.FireAll();

        Console.WriteLine("Done.");
    }
}
*/

public class Program
{
    public static void Main()
    {
        var engine = new ReteEngine();
        var agenda = new Agenda();
        var cellMemory = new AlphaMemory();

        // --- LINKING JOIN AND TERMINAL ---
        var conflictJoin = new JoinNode(cellMemory, cellMemory, (a, b) => {
            var c1 = (Cell)a; var c2 = (Cell)b;
            return c1.Id == c2.Id && c1.Value != c2.Value;
        });

        // High priority rule (Salience 100)
        var terminalNode = new TerminalNode("CriticalConflict", t => {
            var c = (Cell)t.Facts[0];
            Console.WriteLine($"!!! CRITICAL: {c.Id} mismatch detected first.");
        }, agenda, salience: 100);

        conflictJoin.AddSuccessor(terminalNode);

        // --- ASSERTING FACTS ---
        var alphaFilter = new AlphaConditionNode<Cell>(c => c.Value > 0, cellMemory);
        engine.AddAlphaNode(alphaFilter);

        engine.Assert(new Cell { Id = "A1", Value = 100 });
        engine.Assert(new Cell { Id = "A1", Value = 200 });

        // --- FIRING THE AGENDA ---
        agenda.FireAll();
    }
}


public class Cell
{
    public string Id { get; set; }
    public int Value { get; set; }
    public override string ToString() => $"[ID:{Id}, Val:{Value}]";
}
