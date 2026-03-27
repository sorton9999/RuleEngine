using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuleEngineLib
{
    public interface I_Evaluate
    {
        string Name { get; set; }
        bool Evaluate();
        bool REvaluate();
        bool Update(string name, object value);
    }

    public interface I_RuleEvaluation
    {
        bool Evaluate();
        void AddRule(I_Evaluate item);
    }
}
