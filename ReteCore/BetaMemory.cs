using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReteCore
{
    public class BetaMemory : IReteNode
    {
        public List<Token> Tokens { get; } = new();
        private readonly List<IReteNode> _successors = new();

        public void AddSuccessor(IReteNode node) => _successors.Add(node);

        public void Assert(object fact)
        {
            if (fact is Token token)
            {
                if (Tokens.Any(t => t.Equals(token))) { return; }
                Tokens.Add(token);
                foreach (var node in _successors)
                {
                    /*
                    if (node is JoinNode join)
                    {
                        join.LeftAssert(token);
                    }
                    else
                    {
                        node.Assert(token);
                    }
                    */
                    node.Assert(token);
                }
            }
        }

        public void Retract(object fact)
        {
            // Remove tokens containing the retracted fact
            var toRemove = Tokens.Where(t => t.NamedFacts.Values.Contains(fact)).ToList();
            foreach (var token in toRemove)
            {
                Tokens.Remove(token);
                foreach (var node in _successors) node.Retract(fact);
            }
        }

        public void Refresh(object fact, string propertyName)
        {
            var affectedTokens = Tokens.Where(t => t.NamedFacts.Values.Contains(fact)).ToList();

            foreach (var token in affectedTokens)
            {
                foreach (var successor in _successors)
                {
                    if (successor is JoinNode join)
                    {
                        join.Refresh(token, propertyName);
                    }
                    else
                    {
                        successor.Refresh(token, propertyName);
                    }
                }
            }
        }
        public void DebugPrint(object fact, int level = 0)
        {
            string indent = new string(' ', level * 4);
            Console.WriteLine($"{indent}[BetaMemory] - Currently holding {Tokens.Count} partial matches.");
        }
    }
}
