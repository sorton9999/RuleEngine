// See https://aka.ms/new-console-template for more information


using RuleEngineLib;

RuleEngineLib.RuleEngine engine = new RuleEngineLib.RuleEngine();
engine.AddEqualityToRuleset("Equality1", 5, 5, true);
Console.WriteLine($"Name: {engine.RuleNames[0]} [Number of Rules: {engine.RuleCount}] -- Equal? {engine.Evaluate("Equality1")}");

engine.AddInequalityToRuleset("Inequality1", 5, 10, true);
Console.WriteLine($"Name: {engine.RuleNames[1]} [Number of Rules: {engine.RuleCount}] -- Not Equal? {engine.Evaluate("Inequality1")}");

// Add add'l rules
engine.AddEqualityToRuleset("Equality1", 10, 10, true);
Console.WriteLine($"Name: {engine.RuleNames[0]} [Number of Rules: {engine.RuleCount}] -- Equal? {engine.Evaluate("Equality1")}");

engine.AddInequalityToRuleset("Inequality1", 5, 10, true);
Console.WriteLine($"Name: {engine.RuleNames[1]} [Number of Rules: {engine.RuleCount}] -- Not Equal? {engine.Evaluate("Inequality1")}");


// New Block of rules
engine.AddEqualityToRuleset("Equality2", 5, 10, true);
Console.WriteLine($"Name: {engine.RuleNames[2]} [Number of Rules: {engine.RuleCount}] -- Equal? {engine.Evaluate("Equality2")}");

engine.AddInequalityToRuleset("Inequality2", 5, 5, true);
Console.WriteLine($"Name: {engine.RuleNames[3]} [Number of Rules: {engine.RuleCount}] -- Not Equal? {engine.Evaluate("Inequality2")}");

// Add add'l rules
engine.AddEqualityToRuleset("Equality2", 10, 5, true);
Console.WriteLine($"Name: {engine.RuleNames[2]} [Number of Rules: {engine.RuleCount}] -- Equal? {engine.Evaluate("Equality2")}");

engine.AddInequalityToRuleset("Inequality2", 10, 10, true);
Console.WriteLine($"Name: {engine.RuleNames[3]} [Number of Rules: {engine.RuleCount}] -- Not Equal? {engine.Evaluate("Inequality2")}");


// Strings

engine.AddEqualityToRuleset("StringEq1", "My Room", "My Room", true);
Console.WriteLine($"Name: {engine.RuleNames[0]} [Number of Rules: {engine.RuleCount}] -- Equal? {engine.Evaluate("StringEq1")}");

engine.AddInequalityToRuleset("StringIneq1", "My Room", "Your Room", true);
Console.WriteLine($"Name: {engine.RuleNames[1]} [Number of Rules: {engine.RuleCount}] -- Not Equal? {engine.Evaluate("StringIneq1")}");

// Add add'l rules
engine.AddEqualityToRuleset("StringEq1", "His Room", "His Room", true);
Console.WriteLine($"Name: {engine.RuleNames[0]} [Number of Rules: {engine.RuleCount}] -- Equal? {engine.Evaluate("StringEq1")}");

engine.AddInequalityToRuleset("StringIneq1", "Her Room", "His Room", true);
Console.WriteLine($"Name: {engine.RuleNames[1]} [Number of Rules: {engine.RuleCount}] -- Not Equal? {engine.Evaluate("StringIneq1")}");


// New Block of rules
engine.AddEqualityToRuleset("StringEq2", "My Room", "Her Room", true);
Console.WriteLine($"Name: {engine.RuleNames[2]} [Number of Rules: {engine.RuleCount}] -- Equal? {engine.Evaluate("StringEq2")}");

engine.AddInequalityToRuleset("StringIneq2", "His Room", "His Room", true);
Console.WriteLine($"Name: {engine.RuleNames[3]} [Number of Rules: {engine.RuleCount}] -- Not Equal? {engine.Evaluate("StringIneq2")}");

// Add add'l rules
engine.AddEqualityToRuleset("StringEq2", "Her Room", "His Room", true);
Console.WriteLine($"Name: {engine.RuleNames[2]} [Number of Rules: {engine.RuleCount}] -- Equal? {engine.Evaluate("StringEq2")}");

engine.AddInequalityToRuleset("StringIneq2", "Her Room", "Her Room", true);
Console.WriteLine($"Name: {engine.RuleNames[3]} [Number of Rules: {engine.RuleCount}] -- Not Equal? {engine.Evaluate("StringIneq2")}");


List<string> list = new List<string>() { "One", "Two", "Three" };

string srchStr = "One";

Func<object, bool> pred = (theList) =>
{
    return (((List<string>)theList).FindAll((l) => l == srchStr).Count > 0);
};
UnaryRuleItem unaryRuleItem = new UnaryRuleItem(list, pred);
engine.AddToNamedRules("list1", unaryRuleItem);
Console.WriteLine($"Name: list1 [Number of Rules: {engine.RuleCount}] -- Equal? {engine.Evaluate("list1")}");

srchStr = "Three";
Func<object, bool> pred2 = (theList) =>
{
    return (((List<string>)theList).FindAll((l) => l == srchStr).Count > 0);
};
UnaryRuleItem unaryRuleItem2 = new UnaryRuleItem(list, pred2);
engine.AddToNamedRules("list1", unaryRuleItem2);
Console.WriteLine($"Name: list1 [Number of Rules: {engine.RuleCount}] -- Equal? {engine.Evaluate("list1")}");

srchStr = "Four";
Func<object, bool> pred3 = (theList) =>
{
    return (((List<string>)theList).FindAll((l) => l == srchStr).Count > 0);
};
UnaryRuleItem unaryRuleItem3 = new UnaryRuleItem(list, pred3);
engine.AddToNamedRules("list1", unaryRuleItem3);
Console.WriteLine($"Name: list1 [Number of Rules: {engine.RuleCount}] -- Equal? {engine.Evaluate("list1")}");


Console.WriteLine("++++++++++++++++++++++++++++++++ New Rules Engine ++++++++++++++++++++++++++++++++");

//ValueHolder<int>.ValueChanged += OnValueHolderValueChanged;

void OnValueHolderValueChanged(object? sender, ValueChangedEventArgs<int> e)
{
    Console.WriteLine($"Value changed from {e.OldValue} to {e.NewValue}");
}

var service = RuleEngineService.Instance;

service.RegisterRule<int>(new CellRule<int>(new CellRecord<int>(
    "Even Splitter",
    val => val > 0 && val % 2 == 0,
    (val, svc) => {
        Console.WriteLine($"[Rule] {val} is even. Adding {val / 2}...");
        svc.AddCell(val / 2);
    }
)));

// Rule 2: Validation/Alert Rule
service.RegisterRule(new CellRule<int>(new CellRecord<int>(
    "High Value Alert",
    val => val > 1000,
    (val, svc) => Console.WriteLine($"[ALERT] Extreme value detected: {val}")
)));

service.AddCell(10); // Triggers Even Splitter
service.AddCell(5);  // No rules triggered
service.AddCell(1001); // Triggers High Value Alert

int val = 0;
foreach (var cell in service.Cells)
{
    val += (int)cell;
}
Console.WriteLine($"Count Cells[{service.Cells.Count()}]; Final value: {val}");

service.RegisterAutoUpdatingRule<int>("Auto Updating Rule", 5, (val, comp) => val > comp && val % 5 == 0);

service.AddCell(5); // Triggers Auto Updating Rule
service.UpdateRuleValue("Auto Updating Rule", 10); // Updates the comparison value to 10




