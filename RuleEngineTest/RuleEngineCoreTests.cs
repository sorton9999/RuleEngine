// PSEUDOCODE PLAN (detailed):
// - Create unit tests for RuleEngineCore using xUnit.
// - For isolation, construct a fresh RuleEngineService instance (passed into RuleEngineCore ctor).
// - Register elementary rules (equality / inequality) using RuleEngineCore APIs.
// - Register composite rules (AND / OR) to observe published RuleFired events.
// - Use simple local variables (flags, captured arrays) inside onMatch callbacks to assert behavior.
// - Tests to implement:
//   1) EqualityRule_Fires_On_Match:
//      - Register equality rule "eq1" expecting 5 with onMatch toggling a bool.
//      - Register composite OR rule observing "eq1" that captures the fired rule name.
//      - Call service.AddCell(5) and assert both the equality onMatch ran and composite observed "eq1".
//   2) InequalityRule_Fires_On_Match:
//      - Register inequality rule "neq1" unexpected "foo" with onMatch toggle.
//      - Register composite OR rule observing "neq1" to capture fired name.
//      - Call service.AddCell("bar") and assert both onMatch ran and composite observed "neq1".
//   3) CompositeAndRule_Fires_When_All_Fired_In_Either_Order_And_Resets:
//      - Register two equality rules "A" and "B" expecting "A" and "B" respectively.
//      - Register a composite AND rule watching for ["A","B"] that captures the array passed.
//      - Add cells in order A then B and assert composite invoked with both names.
//      - Add cells in reverse order B then A and assert composite invoked again (state reset).
// - Each test uses assertions from xUnit. Keep tests deterministic and synchronous.
// - Assume RuleEngineService has AddCell<T>(T) and behaves synchronously (as used by RuleEngineCore).
//
// The tests below implement the plan as xUnit test methods.

using System;
using Xunit;
using RuleEngineLib;

namespace RuleEngine.Tests
{
    public sealed class RuleEngineCoreTests
    {
        [Fact]
        public void EqualityRule_Fires_On_Match()
        {
            // Arrange
            var service = new RuleEngineService();
            var core = new RuleEngineCore(service);

            bool equalityMatched = false;
            string? observedRule = null;

            core.RegisterEqualityRule<int>("eq1", 5, v => equalityMatched = true);
            core.RegisterCompositeOrRule("orObserver", new[] { "eq1" }, name => observedRule = name);

            // Act
            service.AddCell(5);

            // Assert
            Assert.True(equalityMatched, "Equality rule should have invoked onMatch.");
            Assert.Equal("eq1", observedRule);
        }

        [Fact]
        public void InequalityRule_Fires_On_Match()
        {
            // Arrange
            var service = new RuleEngineService();
            var core = new RuleEngineCore(service);

            bool inequalityMatched = false;
            string? observedRule = null;

            core.RegisterInequalityRule<string>("neq1", "foo", v => inequalityMatched = true);
            core.RegisterCompositeOrRule("orObserver2", new[] { "neq1" }, name => observedRule = name);

            // Act
            service.AddCell("bar");

            // Assert
            Assert.True(inequalityMatched, "Inequality rule should have invoked onMatch for non-matching value.");
            Assert.Equal("neq1", observedRule);
        }

        [Fact]
        public void CompositeAndRule_Fires_When_All_Fired_In_Either_Order_And_Resets()
        {
            // Arrange
            var service = new RuleEngineService();
            var core = new RuleEngineCore(service);

            string[]? capturedFirst = null;
            string[]? capturedSecond = null;

            // Elementary rules publish RuleFired events named "A" and "B".
            core.RegisterEqualityRule<string>("A", "A");
            core.RegisterEqualityRule<string>("B", "B");

            // Composite AND rule should fire when both "A" and "B" have fired.
            core.RegisterCompositeAndRule("andComposite", new[] { "A", "B" }, arr => capturedFirst = arr);

            // Act: fire A then B
            service.AddCell("A");
            service.AddCell("B");

            // Assert after first sequence
            Assert.NotNull(capturedFirst);
            Assert.Contains("A", capturedFirst);
            Assert.Contains("B", capturedFirst);
            Assert.Equal(2, capturedFirst.Length);

            // Prepare for second run: register another composite to capture second invocation.
            core.RegisterCompositeAndRule("andComposite2", new[] { "A", "B" }, arr => capturedSecond = arr);

            // Act: fire in reverse order B then A
            service.AddCell("B");
            service.AddCell("A");

            // Assert after second sequence
            Assert.NotNull(capturedSecond);
            Assert.Contains("A", capturedSecond);
            Assert.Contains("B", capturedSecond);
            Assert.Equal(2, capturedSecond.Length);
        }

        [Fact]
        public void UpdatingRule_Fires_On_ConditionChange()
        {
            var service = new RuleEngineService();
            var core = new RuleEngineCore(service);
            bool ruleFired = false;
            service.RegisterAutoUpdatingRule<int>("autoUpdate", 5, (current, newVal) => current > newVal);
            service.AddCell(10); // Should not fire because 10 (newVal) > 5 (current)
            Assert.False(ruleFired, "Auto-updating rule should not have fired when condition became false.");
            ruleFired = false; // reset for next test
            service.AddCell(3); // Should fire because 5 (current) is > 3 (newVal)
            Assert.True(ruleFired, "Auto-updating rule should have fired when condition is true.");

            service.RegisterAutoUpdatingRule<double>("autoUpdate2", 5, (current, newVal) => current.Equals(newVal));
            service.AddCell(10); // Should not fire because 10 (newVal) does not equal 5 (current)
            Assert.False(ruleFired, "Auto-updating rule should not have fired when condition became false.");
            service.AddCell(5); // Should fire because 5 (current) is > 5 (newVal)
            Assert.True(ruleFired, "Auto-updating rule should have fired when condition is true.");

            string[]? capturedUpdate = null;

            core.RegisterCompositeAndRule("andUpdater", new[] { "autoUpdate", "autoUpdate2" }, arr => capturedUpdate = arr);

            // Assert after first sequence
            Assert.NotNull(capturedUpdate);
            service.AddCell(3); // Should trigger autoUpdate but not autoUpdate2, so composite should not fire
        }
    }
}