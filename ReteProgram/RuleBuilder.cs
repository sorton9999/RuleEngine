using ReteCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReteProgram
{
    // --- The Fluent Builder ---
    public class RuleBuilder<TInitial>
    {
        private readonly ReteEngine _engine;
        private readonly string _ruleName;

        private IReteNode _currentBranchNode;
        private readonly List<IReteNode> _allBranchEnds = new();

        private IReteNode? _lastNode;

        public RuleBuilder(ReteEngine engine, string name) 
        { 
            _engine = engine; 
            _ruleName = name;
            _currentBranchNode = engine.Root;
        }

        public RuleBuilder<TInitial> Match<T>(string name, string? debugLabel = null)
        {
            if (debugLabel != null)
            {
                Console.WriteLine($"===> Start {name}");
            }
            
            var alpha = _engine.GetAlphaMemory<T>();
            var beta = new BetaMemory();
            var adapter = new AlphaToBetaAdapter(beta, name);

            alpha.AddSuccessor(adapter);
            _lastNode = beta;
            return this;
            
            /*
            var alpha = _engine.GetAlphaMemory<T>();
            var adapter = new AlphaToBetaAdapter(new BetaMemory(), name);
            alpha.AddSuccessor(adapter);

            _currentBranchNode.AddSuccessor(alpha);
            _currentBranchNode = adapter;
            return this;
            */
        }

        public RuleBuilder<TInitial> And<T>(string name, Func<Token, T, bool> joinCondition, string? debugLabel = null)
        {
            Func<Token, T, bool> wrapCondition = (token, fact) =>
            {
                bool result = joinCondition(token, fact);
                if (debugLabel != null)
                {
                    Console.WriteLine($"[DEBUG:{debugLabel}] Result: {result} for fact {fact}");
                }
                return result;
            };
            var alpha = _engine.GetAlphaMemory<T>();
            BetaMemory? beta = _lastNode as BetaMemory;
            var join = new JoinNode(beta, alpha, name,
                (token, fact) => wrapCondition(token, (T)fact));

            var nextBeta = new BetaMemory();
            join.AddSuccessor(nextBeta);

            _lastNode = nextBeta;
            return this;
        }

        public RuleBuilder<TInitial> Or()
        {
            _allBranchEnds.Add(_currentBranchNode);
            _currentBranchNode = _engine.Root;
            return this;
        }

        public RuleBuilder<TInitial> Then(Action<Token> action, int salience = 0)
        {
            
            var terminal = new TerminalNode(_ruleName, action, _engine.Agenda, salience);
            if (_lastNode is BetaMemory beta)
            {
                beta.AddSuccessor(terminal);
            }
            return this;
            
            /*
            _allBranchEnds.Add(_currentBranchNode);

            var orNode = new OrNode();

            foreach (var branchEnd in _allBranchEnds)
            {
                branchEnd.AddSuccessor(orNode);
            }

            var terminal = new TerminalNode(_ruleName, action, _engine.Agenda, salience);
            orNode.AddSuccessor(terminal);
            return this;
            */
        }

        public RuleBuilder<TInitial> Trace(string label)
        {
            var tracer = new ReteEngine.TraceNode(label);
            if (_lastNode is BetaMemory beta)
            {
                beta.AddSuccessor(tracer);
            }
            _lastNode = tracer;
            return this;
        }

        public void Assert(object fact)
        {
            _lastNode?.Assert(fact);
        }

        public RuleBuilder<TInitial> StartWith(AlphaMemory alpha, string factName)
        {
            // 1. Create the very first BetaMemory for this rule's chain
            var firstBeta = new BetaMemory();

            // 2. Use the Adapter to convert single facts from Alpha into Tokens for Beta
            var adapter = new AlphaToBetaAdapter(firstBeta, factName);
            
            // 3. Link the AlphaMemory to the Adapter
            alpha.AddSuccessor(adapter);

            // 4. Update the tracker so the next 'JoinWith' knows where to connect
            _lastNode = firstBeta;

            return this;
        }

        public RuleBuilder<TInitial> JoinWith<TNext>(ReteCore.AlphaMemory nextAlpha, Func<Token, TNext, bool> condition)
        {
            BetaMemory? beta = _lastNode as BetaMemory;
            var join = new ReteCore.JoinNode(beta!, nextAlpha, "dummy", (t, f) => condition(t, (TNext)f));
            var nextBeta = new ReteCore.BetaMemory();
            join.AddSuccessor(nextBeta);
            _lastNode = nextBeta;
            return this;
        }

        public void Then(Agenda agenda, Action<Token> action, int salience = 0)
        {
            var terminal = new TerminalNode(_ruleName, action, agenda, salience);
            _lastNode = terminal;
            _lastNode.Assert(new Token("end", 250));
        }
    }

    /// <summary>
    /// Adapts an Alpha Memory output (single fact) to a Beta Memory input (Token).
    /// This allows the first JoinNode in a chain to receive a Token on its left.
    /// </summary>
    public class AlphaToBetaAdapter : IReteNode
    {
        private readonly BetaMemory _betaMemory;
        private readonly string _factName;

        public AlphaToBetaAdapter(BetaMemory betaMemory, string factName)
        {
            _betaMemory = betaMemory ?? throw new ArgumentNullException(nameof(betaMemory));
            _factName = factName;
        }

        public void AddSuccessor(IReteNode node) { }

        public void Assert(object fact)
        {
            // Wrap the single fact into the first Token of a potential chain
            var initialToken = new Token(_factName, fact);
            _betaMemory?.Assert(initialToken);
        }

        public void Retract(object fact)
        {
            // BetaMemory.Retract is already designed to find and remove 
            // any Tokens containing this specific fact.
            _betaMemory?.Retract(fact);
        }

        public void Refresh(object fact, string propertyName)
        {
            _betaMemory?.Refresh(fact, propertyName);
        }
        public void DebugPrint(object fact, int level = 0)
        {
            string indent = new string(' ', level * 4);
            Console.WriteLine($"{indent}[AlphaToBetaAdapter] Wrapping Fact: {fact}");
            _betaMemory?.DebugPrint(fact, level + 1);
        }
    }

    // Test classes
    class SystemStatus
    {
        public string Name { get; set; }
        public bool IsActive { get; set; }
    };

    class Sensor
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsTriggered { get; set; }
    };

    class CriticalCell : Cell
    {
        string _status = String.Empty;
        public string Status { 
            get {  return _status; } 
            set 
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }
    }
}
