using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReteCore
{
    public class Agenda
    {
        private readonly List<Activation> _activations = new();

        public bool HasActivations => _activations.Count > 0;

        public void Add(Activation a) => _activations.Add(a);

        public void RemoveByFact(object fact)
        {
            // Remove any activation where the Match (Token) contains the retracted fact
            //_activations.RemoveAll(a => a.Match.NamedFacts.Values.Contains(fact));
            int removedCount = _activations.RemoveAll(a =>
            a.Match.NamedFacts.Values.Any(f => ReferenceEquals(fact, f)));
            if (removedCount > 0)
            {
                Console.WriteLine($"[AGENDA] Cancelled {removedCount} pending activations.");
            }
        }

        public void FireAll()
        {
            if (!HasActivations) { return; }
            var sorted = _activations.OrderByDescending(a => a.Salience).ToList();
            _activations.Clear();
            foreach (var activation in sorted) { activation.Fire(); }
        }
    }
}
