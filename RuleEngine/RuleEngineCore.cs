using System;
using System.Collections.Generic;
using System.Linq;

namespace RuleEngineLib
{
    /// <summary>
    /// Small event payload used by the service-based rule engine:
    /// elemental rules publish a RuleFired to the engine by calling
    /// <see cref="RuleEngineService.AddCell{T}(T)"/>. Composite rules listen
    /// for <see cref="RuleFired"/> cells and evaluate their logic.
    /// </summary>
    public sealed class RuleFired
    {
        public string RuleName { get; }
        public object? Payload { get; }
        public RuleFired(string ruleName, object? payload = null)
        {
            RuleName = ruleName ?? throw new ArgumentNullException(nameof(ruleName));
            Payload = payload;
        }
    }

    /// <summary>
    /// Convenience rule-building façade that registers rules with the
    /// event-driven <see cref="RuleEngineService"/>. It only uses the
    /// service APIs to create equality/inequality rules and composite
    /// AND / OR rules that tie elementary rules together.
    /// </summary>
    public sealed class RuleEngineCore
    {
        private readonly RuleEngineService _service;

        public RuleEngineCore() : this(RuleEngineService.Instance) { }

        public RuleEngineCore(RuleEngineService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Register a simple equality rule: when a cell of type T arrives and
        /// equals <paramref name="expected"/>, the optional <paramref name="onMatch"/>
        /// callback is invoked and a <see cref="RuleFired"/> event is published.
        /// </summary>
        public void RegisterEqualityRule<T>(string ruleName, T expected, Action<T>? onMatch = null)
        {
            if (ruleName is null) throw new ArgumentNullException(nameof(ruleName));
            Func<T, bool> condition = incoming => EqualityComparer<T>.Default.Equals(incoming, expected);
            Action<T, RuleEngineService> action = (incoming, svc) =>
            {
                try { onMatch?.Invoke(incoming); }
                finally { svc.AddCell(new RuleFired(ruleName, incoming)); }
            };

            var record = new CellRecord<T>(ruleName, condition, action);
            _service.RegisterRule(record);
        }

        /// <summary>
        /// Register a simple inequality rule: when a cell of type T arrives and
        /// does NOT equal <paramref name="unexpected"/>, the optional <paramref name="onMatch"/>
        /// callback is invoked and a <see cref="RuleFired"/> event is published.
        /// </summary>
        public void RegisterInequalityRule<T>(string ruleName, T unexpected, Action<T>? onMatch = null)
        {
            if (ruleName is null) throw new ArgumentNullException(nameof(ruleName));
            Func<T, bool> condition = incoming => !EqualityComparer<T>.Default.Equals(incoming, unexpected);
            Action<T, RuleEngineService> action = (incoming, svc) =>
            {
                try { onMatch?.Invoke(incoming); }
                finally { svc.AddCell(new RuleFired(ruleName, incoming)); }
            };

            var record = new CellRecord<T>(ruleName, condition, action);
            _service.RegisterRule(record);
        }

        /// <summary>
        /// Register a composite AND rule that watches for <see cref="RuleFired"/> cells.
        /// When all required rule names have fired (in any order) the <paramref name="onMatch"/>
        /// callback is invoked once and the internal fired state is cleared.  If any rules have
        /// been evaluated as part of the composite match, the engine will attempt to reset them 
        /// by calling <see cref="RuleEngineService.ResetRuleEvaluation"/>.
        /// </summary>
        public void RegisterCompositeAndRule(string compositeName, IEnumerable<string> requiredRuleNames, Action<string[]?>? onMatch = null)
        {
            if (compositeName is null) throw new ArgumentNullException(nameof(compositeName));
            if (requiredRuleNames is null) throw new ArgumentNullException(nameof(requiredRuleNames));

            var required = requiredRuleNames.Where(n => n is not null).Select(n => n!).Distinct().ToList();
            if (required.Count == 0) throw new ArgumentException("At least one required rule name is required.", nameof(requiredRuleNames));

            var firedSet = new HashSet<string>(StringComparer.Ordinal);
            var locker = new object();

            Func<RuleFired, bool> condition = rf => required.Contains(rf?.RuleName);
            Action<RuleFired, RuleEngineService> action = (rf, svc) =>
            {
                if (rf is null) return;
                lock (locker)
                {
                    firedSet.Add(rf.RuleName);
                    if (required.Any(r => firedSet.Contains(r)))
                    {
                        try
                        {
                            onMatch?.Invoke(required.ToArray());
                        }
                        finally
                        {
                            // reset constituent rules so they can evaluate again
                            foreach (var requiredName in required)
                            {
                                try
                                {
                                    svc.ResetRuleEvaluation(requiredName);
                                }
                                catch
                                { }
                            }
                            firedSet.Clear();
                        }
                    }
                }
            };

            var record = new CellRecord<RuleFired>(compositeName, condition, action);
            _service.RegisterRule(record);
        }

        /// <summary>
        /// Register a composite OR rule that watches for <see cref="RuleFired"/> cells.
        /// When any of the required rule names fires the <paramref name="onMatch"/>
        /// callback is invoked for that rule name.
        /// </summary>
        public void RegisterCompositeOrRule(string compositeName, IEnumerable<string> observedRuleNames, Action<string?>? onMatch = null)
        {
            if (compositeName is null) throw new ArgumentNullException(nameof(compositeName));
            if (observedRuleNames is null) throw new ArgumentNullException(nameof(observedRuleNames));

            var observed = observedRuleNames.Where(n => n is not null).Select(n => n!).Distinct().ToList();
            if (observed.Count == 0) throw new ArgumentException("At least one observed rule name is required.", nameof(observedRuleNames));

            Func<RuleFired, bool> condition = rf => observed.Contains(rf?.RuleName);
            Action<RuleFired, RuleEngineService> action = (rf, svc) =>
            {
                if (rf is null) return;
                onMatch?.Invoke(rf.RuleName);
            };

            var record = new CellRecord<RuleFired>(compositeName, condition, action);
            _service.RegisterRule(record);
        }
    }
}