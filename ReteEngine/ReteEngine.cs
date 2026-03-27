using ReteCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ReteEngine
{

    public class ReteEngine
    {
        private readonly RootNode _root = new();
        private readonly Agenda _agenda = new();
        private readonly List<object> _workingMemory = new();

        // --- Public API ---

        public void Assert(object fact)
        {
            if (!_workingMemory.Contains(fact))
            {
                _workingMemory.Add(fact);
                if (fact is INotifyPropertyChanged observable)
                {
                    observable.PropertyChanged += (s, e) => _root.Refresh(s, e.PropertyName);
                }
                _root.Assert(fact);
            }
        }

        public void Retract(object fact)
        {
            if (_workingMemory.Remove(fact))
            {
                _root.Retract(fact);
            }
        }

        public void FireAll() => _agenda.FireAll();

        // Helper to build a "Conflict" rule easily
        public void RegisterConflictRule<T>(string name, Func<Token, T, bool> condition, Action<T, T> action, int salience = 0)
        {
            var typeNode = new ObjectTypeNode<T>();
            var alphaMem = new AlphaMemory();
            var BetaMem = new BetaMemory();
            var joinNode = new JoinNode(BetaMem, alphaMem, name, (a, b) => condition((Token)a, (T)b));
            var terminal = new TerminalNode(name, t => action((T)t.NamedFacts["A"], (T)t.NamedFacts["B"]), _agenda, salience);

            _root.AddChild(typeNode);
            typeNode.AddChild(alphaMem);
            joinNode.AddSuccessor(terminal);
        }

        public void Update(object fact)
        {
            // Remove the stale state
            this.Retract(fact);
            // Re-evaluate it against all conditions
            this.Assert(fact);
        }

        public ReteEngine()
        {
            // Initialize the Rete network with a root node
            _root.AddChild(new ObjectTypeNode<object>()); // Start with a generic type node
        }

        

        // --- Internal Rete Network Components ---

        private class RootNode : IReteNode
        {
            private readonly List<IReteNode> _children = new();
            public void AddChild(IReteNode node) => _children.Add(node);
            public void Assert(object fact) => _children.ForEach(c => c.Assert(fact));
            public void Retract(object fact) => _children.ForEach(c => c.Retract(fact));
            public void Refresh(object fact, string propertyName)
            {
                foreach (var child in _children)
                {
                    if (child is ObjectTypeNode<object> typeNode)
                    {
                        typeNode.Refresh(fact, propertyName);
                    }
                }
            }
        }

        private class ObjectTypeNode<T> : IReteNode
        {
            private readonly List<IReteNode> _children = new();
            public void AddChild(IReteNode node) => _children.Add(node);
            public void Assert(object fact) { if (fact is T) _children.ForEach(c => c.Assert(fact)); }
            public void Retract(object fact) { if (fact is T) _children.ForEach(c => c.Retract(fact)); }
            public void Refresh(object fact, string propertyName)
            {
                if (fact is T typedFact)
                {
                    foreach (var child in _children)
                    {
                        if (child is AlphaConditionNode<T> alphaNode)
                        {
                            alphaNode.Refresh(typedFact, propertyName);
                        }
                        else
                        {
                            child.Assert(typedFact);
                        }
                    }
                }
            }
        }

        /*
        private class AlphaMemory : IReteNode
        {
            public List<object> Facts { get; } = new();
            private readonly List<JoinNode> _successors = new();
            public void AddSuccessor(JoinNode node) => _successors.Add(node);
            public void Assert(object fact) { Facts.Add(fact); _successors.ForEach(s => s.RightAssert(fact, Facts)); }
            public void Retract(object fact) { if (Facts.Remove(fact)) _successors.ForEach(s => s.RightRetract(fact)); }
            public void Refresh(object fact, string propertyName)
            {
                foreach (var succ in _successors)
                {
                    succ.Refresh(fact, propertyName);
                }
            }
        }

        private class JoinNode
        {
            private readonly Func<object, object, bool> _condition;
            private readonly List<IReteNode> _successors = new();
            public JoinNode(AlphaMemory mem, Func<object, object, bool> cond) { _condition = cond; mem.AddSuccessor(this); }
            public void AddSuccessor(IReteNode node) => _successors.Add(node);
            public void RightAssert(object fact, List<object> memory)
            {
                foreach (var existing in memory)
                {
                    if (!ReferenceEquals(fact, existing) && _condition(existing, fact))
                        _successors.ForEach(s => s.Assert(new Token(existing as Token, "dummy", fact)));
                }
            }
            public void RightRetract(object fact) => _successors.ForEach(s => s.Retract(fact));
            public void Refresh(object factOrToken, string propertyName)
            {
                if (factOrToken is Token token)
                {
                    foreach (var rightFact in _rightInput.Facts)
                    {
                        this.EvaluateAndPropagate(token, propertyName, rightFact);
                    }
                }
                else
                {
                    foreach (var leftToken in _leftInput.Facts)
                    {
                        this.EvaluateAndPropagate(leftToken, propertyName, factOrToken);
                    }
                }
            }

            private void EvaluateAndPropagate(Token left, string newName, object right)
            {
                if (_condition(left, right))
                {
                    var newToken = new Token(left, newName, right);
                    foreach (var s in _successors) { s.Assert(newToken); }
                }
                else
                {
                    foreach (var s in _successors) { s.Retract(right); }
                }
            }
        }

        private class TerminalNode : IReteNode
        {
            private readonly string _name;
            private readonly Action<Token> _action;
            private readonly Agenda _agenda;
            private readonly int _salience;
            public TerminalNode(string n, Action<Token> a, Agenda ag, int s) { _name = n; _action = a; _agenda = ag; _salience = s; }
            public void Assert(object fact) => _agenda.Add(new Activation(_name, _action, (Token)fact, _salience));
            public void Retract(object fact) => _agenda.RemoveByFact(fact);
        }
        */
    }
}
