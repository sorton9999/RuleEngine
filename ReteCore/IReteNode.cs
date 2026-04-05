using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReteCore
{
    /// <summary>
    /// An interface representing a node in a Rete network, which is responsible for processing facts and 
    /// propagating them through the network.
    /// </summary>
    public interface IReteNode
    {
        /// <summary>
        /// Asserts a fact into the Rete node, which may trigger the propagation of the fact to successor 
        /// nodes. The specific behavior of this method depends on the type of node (e.g., AlphaMemory, 
        /// BetaMemory, etc.) and how it processes incoming facts. For example, an AlphaMemory might simply 
        /// store the fact and propagate it to successors, while a BetaMemory might combine it with existing 
        /// tokens to create new matches. The Assert method is a fundamental part of how facts are introduced 
        /// into the Rete network and how they flow through it to trigger rule activations.
        /// </summary>
        /// <param name="fact"></param>
        void Assert(object fact);
        /// <summary>
        /// Retracts a fact from the Rete node, which may trigger the removal of the fact from successor nodes. 
        /// The specific behavior of this method depends on the type of node and how it manages its internal state. 
        /// For example, an AlphaMemory might remove the fact from its collection and propagate the retraction to 
        /// successors, while a BetaMemory might remove any tokens that contain the retracted fact and propagate 
        /// those changes. The Retract method is essential for maintaining the consistency of the Rete network as 
        /// facts change over time, allowing it to correctly reflect the current state of knowledge and trigger or 
        /// deactivate rules as needed.
        /// </summary>
        /// <param name="fact"></param>
        void Retract(object fact);
        /// <summary>
        /// Refreshes a fact in the Rete node, which may trigger re-evaluation of the fact and its propagation to 
        /// successor nodes if necessary. This method is typically called when a property of a fact changes, and it 
        /// allows the Rete network to update its state based on the new information. The specific behavior of this 
        /// method depends on the type of node and how it manages its internal state. For example, an AlphaMemory 
        /// might check if the changed property is relevant to its conditions and re-evaluate the fact accordingly, 
        /// while a BetaMemory might need to re-evaluate any tokens that contain the changed fact. The Refresh method 
        /// is crucial for ensuring that the Rete network remains accurate and responsive to changes in facts over time.
        /// </summary>
        /// <param name="fact"></param>
        /// <param name="propertyName"></param>
        void Refresh(object fact, string propertyName);
        /// <summary>
        /// Visualizes the current state of the Rete node for debugging purposes. This method can be implemented to 
        /// print out the internal state of the node, such as the facts it currently holds, the tokens it has generated, 
        /// or any other relevant information that would help a developer understand how the node is processing facts. 
        /// The level parameter can be used to indicate the depth in the network or to control the amount of detail 
        /// printed, allowing for a more structured and readable output when visualizing complex Rete networks.
        /// </summary>
        /// <param name="fact"></param>
        /// <param name="level"></param>
        void DebugPrint(object fact, int level = 0);
    }
}
