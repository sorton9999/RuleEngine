//-----------------------------------------------------------------------
// <copyright file="AggregationGroupNode.cs">
//     Copyright (c) Steven Orton. All rights reserved.
//     Licensed under the GNU Lesser General Public License v2.1.
//     See LICENSE file in the ReteRaven project root for full license
//     information.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReteCore
{
    /// <summary>
    /// Represents a RETE network node that performs aggregation over groups of facts matching specified join
    /// conditions.
    /// </summary>
    /// <remarks>Aggregates right input facts for each left input token using a provided aggregation function,
    /// and propagates the aggregated result to successor nodes. Manages assertion, retraction, and refresh of facts
    /// while maintaining state for efficient incremental updates.</remarks>
    public class AggregationGroupNode : IReteNode
    {
        /// <summary>
        /// Represents the list of successor nodes in the rete network.
        /// </summary>
        private readonly List<IReteNode> _successors = new();
        /// <summary>
        /// Represents the left input for processing in the latent memory system.
        /// </summary>
        private readonly ILatentMemory _leftInput;
        /// <summary>
        /// Represents the right input for processing in the AlphaMemory.
        /// </summary>
        private readonly AlphaMemory _rightInput;
        /// <summary>
        /// Represents a function that determines whether a join condition is met for a given token and an additional
        /// object.
        /// </summary>
        private readonly Func<Token, object, bool> _joinCondition;
        /// <summary>
        /// A name given to this group.
        /// </summary>
        private readonly string _outputVariableName;
        /// <summary>
        /// This function transforms the raw list of facts into a single scalar value
        /// </summary>
        private readonly Func<IEnumerable<object>, object> _aggregator;
        /// <summary>
        /// Stores a mapping of tokens to their corresponding immutable lists of fact snapshots.
        /// </summary>
        private readonly Dictionary<Token, ImmutableList<FactSnapshot>> _matchedLeftToRight = new();
        /// <summary>
        /// Stores the tokens that are propagated as part of the aggragating operation.
        /// </summary>
        private readonly Dictionary<Token, Token> _lastPropagatedTokens = new();
        /// <summary>
        /// Gets or sets the parent node in the Rete network.
        /// </summary>
        public IReteNode? Parent { get; set; }
        /// <summary>
        /// Gets the collection of successor nodes connected to this node.
        /// </summary>
        public IEnumerable<IReteNode> Successors => _successors;

        /// <summary>
        /// Initializes a new instance of the AggregationGroupNode class for performing aggregation operations with
        /// specified inputs, join condition, output variable, and aggregation strategy.
        /// </summary>
        /// <param name="leftInput">The left input memory source for the aggregation.</param>
        /// <param name="rightInput">The right input memory source for the aggregation.</param>
        /// <param name="joinCondition">A function that determines whether a token and an object should be joined.</param>
        /// <param name="outputVariableName">The name of the variable that will store the aggregation result.</param>
        /// <param name="aggregator">A function that aggregates a collection of objects into a single result.</param>
        public AggregationGroupNode(
            ILatentMemory leftInput,
            AlphaMemory rightInput,
            Func<Token, object, bool> joinCondition,
            string outputVariableName,
            Func<IEnumerable<object>, object> aggregator) // <-- Injected strategy
        {
            _leftInput = leftInput;
            _rightInput = rightInput;
            _joinCondition = joinCondition;
            _outputVariableName = outputVariableName;
            _aggregator = aggregator;
        }

        /// <summary>
        /// Evaluates the specified fact against the left input tokens and updates the matched results accordingly.
        /// </summary>
        /// <param name="fact">The fact to evaluate and assert within the aggregation group.</param>
        public void Assert(object fact)
        {
            if (fact is Token leftToken)
            {
                var matches = _rightInput.Facts
                    .Where(rightFact => _joinCondition(leftToken, rightFact))
                    .Select(rightFact => new FactSnapshot(rightFact, _aggregator(new[] { rightFact })))
                    .ToImmutableList();
                _matchedLeftToRight[leftToken] = matches;
                UpdateAndPropagate(leftToken, matches);
            }
            else
            {
                foreach (var token in _leftInput.Tokens)
                {
                    if (_joinCondition(token, fact))
                    {
                        var currentSnapshot = GetOrCreateMatchList(token);

                        // Create a frozen state record for this item right now
                        var singleValue = _aggregator(new[] { fact });
                        var newSnapshot = new FactSnapshot(fact, singleValue);

                        var updatedList = currentSnapshot.Add(newSnapshot);
                        _matchedLeftToRight[token] = updatedList;
                        UpdateAndPropagate(token, updatedList);
                    }
                }
            }
        }

        /// <summary>
        /// Retracts a fact from the node, handling both left and right retractions depending on the type of the
        /// provided fact.
        /// </summary>
        /// <param name="fact">The fact to retract, which can be a Token for left retraction or an object associated with a left input
        /// token for right retraction.</param>
        public void Retract(object fact)
        {
            if (fact is Token leftToken)
            {
                // Left Retraction: The entire match parent path is broken
                if (_lastPropagatedTokens.TryGetValue(leftToken, out var lastToken))
                {
                    foreach (var successor in _successors) { successor.Retract(lastToken); }
                    _lastPropagatedTokens.Remove(leftToken);
                }
                _matchedLeftToRight.Remove(leftToken);
            }
            else
            {
                // Right Retraction: An individual item was deleted
                foreach (var token in _leftInput.Tokens)
                {
                    if (_matchedLeftToRight.TryGetValue(token, out var list))
                    {
                        var targetSnapshot = list.FirstOrDefault(m => m.Reference == fact);
                        if (targetSnapshot.Reference == null) continue; // No match found for this token
                        var updatedList = list.Remove(targetSnapshot);
                        _matchedLeftToRight[token] = updatedList;

                        // FIX: propagate using the updated list (was passing the old list previously)
                        UpdateAndPropagate(token, updatedList);
                    }
                }
            }
        }

        /// <summary>
        /// Updates token state and propagates changes based on the specified fact and property name.
        /// </summary>
        /// <param name="fact">The fact object to process for state refresh.</param>
        /// <param name="propertyName">The name of the property associated with the fact.</param>
        public void Refresh(object fact, string propertyName)
        {
            if (fact is Token leftToken)
            {
                Retract(leftToken);
                Assert(leftToken);
                return;
            }

            foreach (var token in _leftInput.Tokens)
            {
                var currentSnapshot = GetOrCreateMatchList(token);

                // Match based on the stable reference pointer
                var historicalRecord = currentSnapshot.FirstOrDefault(m => m.Reference == fact);
                bool wasMatched = historicalRecord.Reference != null;
                bool nowMatches = _joinCondition(token, fact);
                if (wasMatched && !nowMatches)
                {
                    // Generates a new frozen slice without the item
                    var updatedList = currentSnapshot.Remove(historicalRecord);
                    _matchedLeftToRight[token] = updatedList;
                    UpdateAndPropagate(token, updatedList);
                }
                else if (!wasMatched && nowMatches)
                {
                    var singleValue = _aggregator(new[] { fact });
                    var updatedList = currentSnapshot.Add(new FactSnapshot(fact, singleValue));
                    _matchedLeftToRight[token] = updatedList;
                    UpdateAndPropagate(token, updatedList);
                }
                else if (wasMatched && nowMatches)
                {

                    UpdateAndPropagateOnRefresh(token, fact, currentSnapshot);
                }
            }
        }

        /// <summary>
        /// Updates the aggregation state for the specified token and fact, recalculates the aggregate value, and
        /// propagates changes to successor nodes if the result has changed.
        /// </summary>
        /// <param name="leftToken">The token representing the left side of the join condition.</param>
        /// <param name="rightFact">The fact from the right input to evaluate and aggregate.</param>
        /// <param name="oldSnapshot">The previous immutable list of fact snapshots before the update.</param>
        private void UpdateAndPropagateOnRefresh(Token leftToken, object rightFact, ImmutableList<FactSnapshot> oldSnapshot)
        {
            var currentSnapshot = GetOrCreateMatchList(leftToken);
            var historicalRecord = currentSnapshot.FirstOrDefault(m => m.Reference == rightFact);

            object newResult = _aggregator(_rightInput.Facts.Where(rf => _joinCondition(leftToken, rf)).ToImmutableList());

            object sampleVal = historicalRecord.CalculatedValue;
            object oldResult;

            // Take care of the cases where we have values as primitive types
            if (sampleVal is int oldInt && newResult is int newInt)
            {
                var freshSingleValue = _aggregator(new[] { rightFact });
                int currentWeight = freshSingleValue is int cw ? cw : 1;
                int pastWeight = oldInt;

                bool isCountOperation = freshSingleValue is int fv && fv == 1 && _aggregator(new object[0]) is int ev && ev == 0;

                if (isCountOperation)
                {
                    oldResult = newInt;
                }
                else
                {
                    oldResult = newInt - currentWeight + pastWeight;
                }

            }
            else if (sampleVal is decimal oldDec && newResult is decimal newDec)
            {
                var freshSingleValue = _aggregator(new[] {rightFact});
                decimal currentWeight = freshSingleValue is decimal cw ? cw : 1m;
                oldResult = newDec - currentWeight + oldDec;
            }
            else
            {
                oldResult = historicalRecord.CalculatedValue;
            }

            if (oldResult.Equals(newResult))
            {
                return;
            }

            // Commit the new baseline to memory state tracking
            var newSingleValue = _aggregator(new[] { rightFact });
            var updatedList = currentSnapshot.Replace(historicalRecord, new FactSnapshot(rightFact, newSingleValue));
            _matchedLeftToRight[leftToken] = updatedList;

            // Swap the tokens downstream
            if (_lastPropagatedTokens.TryGetValue(leftToken, out var oldToken))
            {
                foreach (var successor in _successors) { successor.Retract(oldToken); }
            }

            var newToken = new Token(leftToken, _outputVariableName, newResult);
            _lastPropagatedTokens[leftToken] = newToken;

            foreach (var successor in _successors) { successor.Assert(newToken); }
        }

        /// <summary>
        /// Updates the aggregation state with the specified token and current matches, retracts any previously
        /// propagated tokens, and asserts the new token to all successors.
        /// </summary>
        /// <param name="leftToken">The token representing the current state to update and propagate.</param>
        /// <param name="currentMatches">The collection of fact snapshots used to compute the new aggregation result.</param>
        private void UpdateAndPropagate(Token leftToken, IEnumerable<FactSnapshot> currentMatches)
        {
            object scalarResult = _aggregator(currentMatches.Select(m => m.Reference));

            if (_lastPropagatedTokens.TryGetValue(leftToken, out var oldToken))
            {
                foreach (var successor in _successors) { successor.Retract(oldToken); }
            }

            var newToken = new Token(leftToken, _outputVariableName, scalarResult);
            _lastPropagatedTokens[leftToken] = newToken;

            foreach (var successor in _successors) { successor.Assert(newToken); }
        }

        /// <summary>
        /// Retrieves the existing match list for the specified token or creates a new empty list if none exists.
        /// </summary>
        /// <param name="leftToken">The token used to identify the match list.</param>
        /// <returns>An immutable list of fact snapshots associated with the specified token.</returns>
        private ImmutableList<FactSnapshot> GetOrCreateMatchList(Token leftToken)
        {
            if (!_matchedLeftToRight.TryGetValue(leftToken, out var list)) 
            { 
                list = ImmutableList<FactSnapshot>.Empty; 
                _matchedLeftToRight[leftToken] = list; 
            }
            return list;
        }

        /// <summary>
        /// Adds the specified node as a successor and sets its parent to the current node.
        /// </summary>
        /// <param name="node">The node to add as a successor.</param>
        public void AddSuccessor(IReteNode node) { node.Parent = this; _successors.Add(node); }

        /// <summary>
        /// Removes the specified successor node from the collection.
        /// </summary>
        /// <param name="node">The successor node to remove.</param>
        public void RemoveSuccessor(IReteNode node) => _successors.Remove(node);

        /// <summary>
        /// Writes the specified object to the debug output with optional indentation.
        /// </summary>
        /// <param name="fact">The object to write to the debug output.</param>
        /// <param name="level">The indentation level to apply to the output.</param>
        public void DebugPrint(object fact, int level = 0) { }
    }

    /// <summary>
    /// A snapshot of a fact's state at a given point in time, 
    /// capturing both the reference to the fact and its calculated value.
    /// </summary>
    readonly struct FactSnapshot
    {
        /// <summary>
        /// The fact reference object that is one part of the aggregation operation
        /// </summary>
        public object Reference { get; }
        /// <summary>
        /// The calculated value to store with the fact as part of the aggregation operation.
        /// </summary>
        public object CalculatedValue { get; }
        /// <summary>
        /// Initializes a new instance of the FactSnapshot class.
        /// </summary>
        /// <param name="reference">The reference object associated with the snapshot.</param>
        /// <param name="calculatedValue">The calculated value associated with the snapshot.</param>
        public FactSnapshot(object reference, object calculatedValue)
        {
            Reference = reference;
            CalculatedValue = calculatedValue;
        }
    }

}

