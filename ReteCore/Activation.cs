using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReteCore
{
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
}
