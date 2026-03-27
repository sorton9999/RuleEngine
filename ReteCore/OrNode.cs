using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReteCore
{
    public class OrNode : IReteNode
    {
        private readonly List<IReteNode> _successors = new();

        public void AddSuccessor(IReteNode node) => _successors.Add(node);

        // The OrNode just acts as a passthrough for multiple branches
        public void Assert(object fact)
        {
            foreach (var successor in _successors)
            {
                successor.Assert(fact);
            }
        }

        public void Retract(object fact)
        {
            foreach (var successor in _successors) { successor.Retract(fact); }
        }

        public void Refresh(object fact, string prop)
        {
            foreach (var successor in _successors) { successor.Refresh(fact, prop); }
        }

        public void DebugPrint(object fact, int level = 0)
        {
            string indent = new string(' ', level * 3);
            Console.WriteLine($"{indent}[OrNode] - Currently holding {_successors.Count} successors. Fact[{fact}]");
        }
    }
}
