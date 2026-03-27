using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReteCore
{
    public interface IReteNode
    {
        void Assert(object fact);
        void Retract(object fact);
        void Refresh(object fact, string propertyName);
        //void AddSuccessor(IReteNode node);

        // New: Visualizes the path of a specific fact
        void DebugPrint(object fact, int level = 0);
    }
}
