using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ReteCore
{
    public class AlphaMemory : IReteNode
    {
        public List<object> Facts { get; } = new();
        private readonly List<IReteNode> _successors = new();

        public void AddSuccessor(IReteNode node) => _successors.Add(node);

        public void Assert(object fact)
        {
            if (!Facts.Contains(fact))
            {
                Facts.Add(fact);

                foreach (var succ in _successors)
                {
                    succ.Assert(fact);
                }
            }
        }

        public void Retract(object fact)
        {
            if (Facts.Remove(fact))
            {
                // Tell successors this fact is gone
                foreach (var succ in _successors)
                {
                    succ.Retract(fact);
                }
            }
        }

        public void Refresh(object fact, string propertyName)
        {
            if (Facts.Contains(fact))
            {
                foreach (var succ in _successors)
                {
                    succ.Refresh(fact, propertyName);
                }
            }
        }

        public void DebugPrint(object fact, int level = 0)
        {
            string indent = new string(' ', level * 4);
            bool contains = Facts.Contains(fact);
            Console.WriteLine($"{indent}[AlphaMemory] - Fact present: {contains}. Total facts stored: {Facts.Count}");
        }
    }
}
