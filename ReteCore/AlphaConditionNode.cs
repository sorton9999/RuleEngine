using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReteCore
{
    public class AlphaConditionNode<T> : IReteNode
    {
        private readonly Func<T, bool> _predicate;
        private readonly IReteNode _successor; // Usually an AlphaMemory

        public AlphaConditionNode(string propertytName, Func<T, bool> predicate, IReteNode successor)
        {
            TargetProperty = propertytName;
            _predicate = predicate;
            _successor = successor;
        }

        public string TargetProperty { get; }

        public void AddSuccessor(IReteNode node) { }

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

        /// <summary>
        /// Selective re-evaluation
        /// </summary>
        /// <param name="fact"></param>
        /// <param name="changedProperty"></param>
        public void Refresh(object fact, string changedProperty)
        {
            // Only re-run the filter if the changed property matches
            // or is unknown
            if (string.IsNullOrEmpty(changedProperty) || changedProperty == TargetProperty)
            {
                // Remove the old state to avoid duplicates
                _successor.Retract(fact);
                // Check new state
                this.Assert(fact);
            }
        }
        public void DebugPrint(object fact, int level = 0)
        {
            string indent = new string(' ', level * 2);
            if (fact is T typedFact)
            {
                bool pass = _predicate(typedFact);
                Console.WriteLine($"{indent}[AlphaNode:{TargetProperty}] {(pass ? "PASS" : "FAIL")}");
                if (pass) //foreach (var child in _children) child.DebugPrint(fact, level + 1);
                    _successor.DebugPrint(fact, level + 1);
            }
        }
    }
}
