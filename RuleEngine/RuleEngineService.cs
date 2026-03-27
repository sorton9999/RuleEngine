using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace RuleEngineLib
{
    /*
     * Plan (pseudocode, detailed, event-driven)
     *
     * 1. Make cell additions and holder updates event-driven:
     *    - Expose a `CellAdded` event on `RuleEngineService`.
     *    - Add a `ValueChanged` event on `ValueHolder<T>`.
     *
     * 2. When registering a rule, subscribe a handler to `CellAdded` that calls
     *    `rule.TryEvaluate(e.Cell, this)` so evaluation is driven by the event.
     *
     * 3. For auto-updating rules:
     *    - Create a `ValueHolder<T>` with a `ValueChanged` event.
     *    - The rule's action sets `holder.Value = incoming` (this raises ValueChanged).
     *    - Subscribe to `holder.ValueChanged` to reset `rule.Evaluated = false` under the lock.
     *
     * 4. Keep mutations of `_rules` and `_holders` under `_lockObject`.
     *
     * 5. When adding a cell:
     *    - Add to `_cells` under the lock.
     *    - Raise `CellAdded` after releasing the lock so subscribed rules evaluate.
     *
     * 6. Preserve existing public API: RegisterRule, RegisterRule(CellRule<T>),
     *    RegisterAutoUpdatingRule, AddCell, UpdateRuleValue, ResetRuleEvaluation.
     *
     * 7. Thread-safety:
     *    - All changes to `_rules` and `_holders` occur while holding `_lockObject`.
     *    - Setting `rule.Evaluated` from the holder event is performed with `_lockObject`.
     *
     * This keeps the engine event-driven while minimizing changes to the existing types.
     */

    /// <summary>
    /// Event args for a newly added cell.
    /// </summary>
    public sealed class CellAddedEventArgs : EventArgs
    {
        public object Cell { get; }
        public CellAddedEventArgs(object cell) => Cell = cell ?? throw new ArgumentNullException(nameof(cell));
    }

    /// <summary>
    /// The main Rule service. This non-generic service can store and evaluate rules for multiple
    /// cell types within a single service instance. It is event-driven: rules subscribe to cell additions.
    /// </summary>
    public sealed class RuleEngineService
    {
        private static readonly Lazy<RuleEngineService> _instance = new(() => new RuleEngineService());
        public static RuleEngineService Instance => _instance.Value;

        private readonly ConcurrentBag<object> _cells = new();
        private readonly List<ICellRule> _rules = new();

        // holders keyed by rule name so external updates are possible
        private readonly Dictionary<string, object> _holders = new();

        private static readonly object _lockObject = new();

        /// <summary>
        /// Raised when a cell is added to the engine. Rules subscribe to this to evaluate.
        /// </summary>
        public event EventHandler<CellAddedEventArgs>? CellAdded;

        public IEnumerable<object> Cells => _cells;

        public IReadOnlyCollection<object> CellSnapshot => _cells.ToArray();

        /// <summary>
        /// Register a rule record for type T.
        /// Rule will be subscribed to the CellAdded event so it evaluates when a new cell is added.
        /// </summary>
        public void RegisterRule<T>(CellRecord<T> record)
        {
            if (record is null) throw new ArgumentNullException(nameof(record));
            var rule = new CellRule<T>(record);
            RegisterRule(rule);
        }

        /// <summary>
        /// Register a pre-built CellRule{T}.
        /// The rule will be subscribed to the CellAdded event.
        /// </summary>
        public void RegisterRule<T>(CellRule<T> rule)
        {
            if (rule is null) throw new ArgumentNullException(nameof(rule));
            lock (_lockObject)
            {
                _rules.Add(rule);
                SubscribeRuleToCellAdded(rule);
            }
        }

        /// <summary>
        /// Register an auto-updating comparison rule.
        /// The rule holds an internal value (initialValue). For each incoming value the comparator is invoked:
        ///     comparator(currentHeldValue, incoming)
        /// If true, the rule's action will set the held value = incoming (raising ValueChanged) and allow
        /// the rule to evaluate again later (ValueChanged handler resets Evaluated).
        /// The rule is identified by 'name' so you can update its held value later via UpdateRuleValue.
        /// </summary>
        public void RegisterAutoUpdatingRule<T>(string name, T initialValue, Func<T, T, bool> comparator)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            if (comparator is null) throw new ArgumentNullException(nameof(comparator));

            // holder for the mutable stored value (raises ValueChanged)
            var holder = new ValueHolder<T>(initialValue);

            // create an empty CellRule so the action can capture and reset its Evaluated flag via holder events
            var rule = new CellRule<T>();

            // condition uses the current value in holder
            Func<T, bool> condition = incoming => comparator(holder.Value, incoming);

            // action updates the holder (this will raise ValueChanged)
            Action<T, RuleEngineService> action = (incoming, svc) =>
            {
                holder.Value = incoming;
            };

            var record = new CellRecord<T>(name, condition, action);

            // assign the record to the rule and register
            rule.CellRecord = record;

            lock (_lockObject)
            {
                _rules.Add(rule);
                _holders[name] = holder!;
                // subscribe holder value changes to reset rule evaluation state under lock
                holder.ValueChanged += (_, __) =>
                {
                    lock (_lockObject)
                    {
                        rule.Evaluated = false;
                    }
                };
                // subscribe the rule to cell additions
                SubscribeRuleToCellAdded(rule);
            }
        }

        /// <summary>
        /// Update the held value for an auto-updating rule registered with RegisterAutoUpdatingRule.
        /// </summary>
        public bool UpdateRuleValue<T>(string name, T newValue)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            lock (_lockObject)
            {
                if (_holders.TryGetValue(name, out var obj) && obj is ValueHolder<T> holder)
                {
                    holder.Value = newValue!;
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Reset the Evaluated flag for a rule with the given name so it can run again.
        /// </summary>
        public bool ResetRuleEvaluation(string name)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            lock (_lockObject)
            {
                foreach (var r in _rules)
                {
                    // match by CellRecord.Name if available
                    var rr = GetCellRecordName(r);
                    if (rr == name)
                    {
                        r.Evaluated = false;
                        return true;
                    }
                }
                return false;
            }
        }

        private static string? GetCellRecordName(ICellRule rule)
        {
            // try reflection-free extraction: many rules are CellRule<T>
            if (rule is CellRule<object> co && co.CellRecord is not null) return co.CellRecord.Name;
            // fallback: use pattern matching to extract generic CellRule<T>
            var ruleType = rule.GetType();
            var prop = ruleType.GetProperty(nameof(CellRule<object>.CellRecord));
            if (prop is not null)
            {
                var rec = prop.GetValue(rule);
                if (rec is not null)
                {
                    var nameProp = rec.GetType().GetProperty(nameof(CellRecord<object>.Name));
                    if (nameProp is not null)
                    {
                        return nameProp.GetValue(rec) as string;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Add a cell of any type. Stored as object and published to the CellAdded event so subscribed rules evaluate.
        /// </summary>
        public void AddCell<T>(T value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            EventHandler<CellAddedEventArgs>? handlersSnapshot;
            lock (_lockObject)
            {
                _cells.Add(value);
                handlersSnapshot = CellAdded;
            }

            // invoke event outside the lock
            handlersSnapshot?.Invoke(this, new CellAddedEventArgs(value!));
        }

        private void SubscribeRuleToCellAdded(ICellRule rule)
        {
            // subscribe a handler that attempts to evaluate the rule when a cell is added
            CellAdded += (_, e) =>
            {
                // TryEvaluate may mutate rule state; that mutation is handled inside TryEvaluate
                // and ValueChanged events reset Evaluated under lock when needed.
                rule.TryEvaluate(e.Cell, this);
            };
        }
    }

    internal interface ICellRule
    {
        bool Evaluated { get; set; }
        /// <summary>
        /// Attempt to evaluate this rule for the given value. Returns true if the rule executed.
        /// </summary>
        bool TryEvaluate(object value, RuleEngineService service);
    }

    /// <summary>
    /// A rule wrapper for a specific cell type T.
    /// </summary>
    public class CellRule<T> : ICellRule
    {
        public bool Evaluated { get; set; }

        // made settable so we can construct a CellRule first and then assign a CellRecord
        public CellRecord<T>? CellRecord { get; set; }

        public CellRule()
        {
        }

        public CellRule(CellRecord<T>? record)
        {
            CellRecord = record;
        }

        public bool TryEvaluate(object value, RuleEngineService service)
        {
            if (Evaluated || CellRecord is null) return false;

            if (value is not T typed) return false;

            if (CellRecord.Condition(typed))
            {
                Evaluated = true;
                CellRecord.Action(typed, service);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// A reusable rule definition for a specific type T.
    /// </summary>
    public record CellRecord<T>(
        string Name,
        Func<T, bool> Condition,
        Action<T, RuleEngineService> Action
    );

    // event args for ValueHolder changes
    public sealed class ValueChangedEventArgs<T> : EventArgs
    {
        public T NewValue { get; }
        public T? OldValue { get; }
        public ValueChangedEventArgs(T newValue, T? oldValue = default) { NewValue = newValue; OldValue = oldValue; }
    }

    // simple mutable holder used by auto-updating rules; raises ValueChanged when the value changes
    public sealed class ValueHolder<T>
    {
        private T _value;
        private T? _oldValue;
        public event EventHandler<ValueChangedEventArgs<T>>? ValueChanged;

        public ValueHolder(T value) => _value = value;

        public T Value
        {
            get => _value;
            set
            {
                if (!EqualityComparer<T>.Default.Equals(_value, value))
                {
                    _oldValue = _value;
                    _value = value;
                    ValueChanged?.Invoke(this, new ValueChangedEventArgs<T>(value, _oldValue));
                }
            }
        }

        public T? OldValue => _oldValue;
    }
}



