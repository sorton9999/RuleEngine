// Plan (pseudocode, detailed)
//
// 1. Create isolated RuleEngineService instances for each test to avoid shared mutable state.
// 2. Provide helper reflection utilities:
//    - ClearValueHolderEvent<T>() to remove any static event handlers on ValueHolder<T> (ensures isolation).
//    - GetHolderValue<T>(service, name) to inspect the internal holder value stored in the service.
// 3. Write tests covering:
//    - RegisterRule using CellRecord<T>: verifies action runs only when condition matches and respects Evaluated.
//    - RegisterRule using pre-built CellRule<T>: validates same behavior for pre-built rules.
//    - RegisterAutoUpdatingRule: validates the holder is updated only when comparator returns true and that subsequent evaluations update the holder.
//    - UpdateRuleValue: ensures external updates change the held value.
//    - Wrong-type cells do not trigger rules.
//    - Cells and CellSnapshot contain added values.
// 4. Use reflection to inspect private _holders dictionary to assert holder values.
// 5. Disable parallel test execution to avoid cross-test interference (static events).
//
// Tests use xUnit and make assertions with Xunit.Assert.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RuleEngineLib;
using Xunit;

// Disable parallelization at assembly level to avoid static event cross-test interference.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RuleEngine.Tests
{
    public class RuleEngineServiceTests
    {
        // Reflection helper: clear static ValueHolder<T>.ValueChanged event handlers
        private static void ClearValueHolderEvent<T>()
        {
            var evtField = typeof(ValueHolder<T>).GetField("ValueChanged", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (evtField is not null)
            {
                evtField.SetValue(null, null);
            }
        }

        // Reflection helper: read internal holder value from service._holders by name
        private static T? GetHolderValue<T>(RuleEngineService service, string name)
        {
            var holdersField = typeof(RuleEngineService).GetField("_holders", BindingFlags.Instance | BindingFlags.NonPublic);
            if (holdersField is null) throw new InvalidOperationException("Unable to find _holders field via reflection.");

            var dict = holdersField.GetValue(service) as IDictionary<string, object>;
            if (dict is null) throw new InvalidOperationException("_holders is not the expected type.");

            if (!dict.TryGetValue(name, out var holderObj)) return default;

            if (holderObj is ValueHolder<T> holder) return holder.Value;

            throw new InvalidOperationException($"Holder with name '{name}' is not a ValueHolder<{typeof(T).Name}>.");
        }

        [Fact]
        public void RegisterRule_CellRecord_ActionRunsOnMatchingCellAndRespectsEvaluated()
        {
            var svc = new RuleEngineService();
            var executed = 0;

            var record = new CellRecord<int>(
                "greaterThanTen",
                Condition: v => v > 10,
                Action: (v, s) => { executed++; }
            );

            svc.RegisterRule(record);

            svc.AddCell(5);   // not match
            Assert.Equal(0, executed);

            svc.AddCell(15);  // match -> executes once
            Assert.Equal(1, executed);

            svc.AddCell(20);  // rule already Evaluated -> should not execute again
            Assert.Equal(1, executed);

            // Reset evaluation and add another matching cell
            var reset = svc.ResetRuleEvaluation("greaterThanTen");
            Assert.True(reset);

            svc.AddCell(25);
            Assert.Equal(2, executed);
        }

        [Fact]
        public void RegisterRule_PrebuiltCellRule_TriggersSameAsRecord()
        {
            var svc = new RuleEngineService();
            var triggeredValues = new List<string>();

            var record = new CellRecord<string>(
                "startsWithA",
                Condition: s => s.StartsWith("A", StringComparison.Ordinal),
                Action: (s, svc2) => triggeredValues.Add(s)
            );

            var rule = new CellRule<string>(record);
            svc.RegisterRule(rule);

            svc.AddCell("Banana");
            Assert.Empty(triggeredValues);

            svc.AddCell("Apple");
            Assert.Single(triggeredValues);
            Assert.Contains("Apple", triggeredValues);

            // rule should be Evaluated; adding another matching value should not add again
            svc.AddCell("Apricot");
            Assert.Single(triggeredValues);

            // reset and add another
            Assert.True(svc.ResetRuleEvaluation("startsWithA"));
            svc.AddCell("Apricot");
            Assert.Equal(2, triggeredValues.Count);
        }

        [Fact]
        public void RegisterAutoUpdatingRule_UpdatesHolderOnlyWhenComparatorTrue()
        {
            ClearValueHolderEvent<int>();
            var svc = new RuleEngineService();

            // comparator: incoming must be greater than current
            svc.RegisterAutoUpdatingRule<int>("maxValue", initialValue: 10, comparator: (current, incoming) => incoming > current);

            // initial holder should be 10
            Assert.Equal(10, GetHolderValue<int>(svc, "maxValue"));

            svc.AddCell(5);   // smaller -> no update
            Assert.Equal(10, GetHolderValue<int>(svc, "maxValue"));

            svc.AddCell(15);  // bigger -> updates to 15
            Assert.Equal(15, GetHolderValue<int>(svc, "maxValue"));

            svc.AddCell(12);  // smaller than 15, no update
            Assert.Equal(15, GetHolderValue<int>(svc, "maxValue"));

            svc.AddCell(20);  // updates to 20
            Assert.Equal(20, GetHolderValue<int>(svc, "maxValue"));
        }

        [Fact]
        public void UpdateRuleValue_ExternallySetsHolderValue()
        {
            ClearValueHolderEvent<int>();
            var svc = new RuleEngineService();
            svc.RegisterAutoUpdatingRule<int>("target", initialValue: 1, comparator: (curr, inc) => inc > curr);

            // externally set to a new value
            var updated = svc.UpdateRuleValue<int>("target", 42);
            Assert.True(updated);

            Assert.Equal(42, GetHolderValue<int>(svc, "target"));
        }

        [Fact]
        public void AddCell_WrongTypeDoesNotTriggerRule()
        {
            var svc = new RuleEngineService();
            var executed = false;

            var record = new CellRecord<int>(
                "positive",
                Condition: v => v > 0,
                Action: (v, s) => executed = true
            );

            svc.RegisterRule(record);

            // add a string cell; should not trigger int rule
            svc.AddCell("not-an-int");

            Assert.False(executed);
        }

        [Fact]
        public void CellsAndCellSnapshot_ContainAddedValues()
        {
            var svc = new RuleEngineService();
            svc.AddCell(1);
            svc.AddCell("two");
            svc.AddCell(3.0);

            var all = svc.Cells.ToList();
            Assert.Contains(1, all);
            Assert.Contains("two", all);
            Assert.Contains(3.0, all);

            var snap = svc.CellSnapshot;
            Assert.Equal(3, snap.Count);
            Assert.Contains("two", snap);
        }
    }
}