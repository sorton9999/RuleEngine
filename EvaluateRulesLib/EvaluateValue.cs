using System.Collections.Concurrent;

namespace EvaluateRulesLib
{

    public sealed class FastCellService<T>
    {
        private static readonly Lazy<FastCellService<T>> _instance = new(() => new FastCellService<T>());
        public static FastCellService<T> Instance => _instance.Value;

        private readonly ConcurrentBag<T> _cells = new();
        private readonly List<CellRule<T>> _rules = new();

        public IEnumerable<T> Cells => _cells;

        // Register a rule (e.g., "If value > 100, add a bonus cell")
        public void RegisterRule(CellRule<T> rule) => _rules.Add(rule);

        public void AddCell(T value)
        {
            _cells.Add(value);

            // Automatic Rules Engine Execution
            foreach (var rule in _rules)
            {
                if (rule.Condition(value))
                {
                    rule.Action(value, this);
                }
            }
        }
    }

    public record CellRule<T>(
        string Name,
        Func<T, bool> Condition,
        Action<T, FastCellService<T>> Action
    );


    public class EvaluateValue<T>  where T : notnull
    {
        T Value {  get; set; }

        public EvaluateValue(T Value)
        {
            this.Value = Value;
        }

        public static bool? operator |(EvaluateValue<T> value1, EvaluateValue<T> value2)
        {
            return value1 | value2;
        }

        public static bool? operator &(EvaluateValue<T> value1, EvaluateValue<T> value2)
        {
            return value1 & value2;
        }

        public static EvaluateValue<T> operator !(EvaluateValue<T> value1)
        {
            return !value1;
        }

        public void EvaluateExample()
        {
            var service = FastCellService<int>.Instance;

            // Rule 1: If a cell value is even, automatically add its half as a new cell
            service.RegisterRule(new CellRule<int>(
                "Even Splitter",
                val => val > 0 && val % 2 == 0,
                (val, svc) => {
                    Console.WriteLine($"[Rule] {val} is even. Adding {val / 2}...");
                    svc.AddCell(val / 2);
                }
            ));

            // Rule 2: Validation/Alert Rule
            service.RegisterRule(new CellRule<int>(
                "High Value Alert",
                val => val > 1000,
                (val, svc) => Console.WriteLine($"[ALERT] Extreme value detected: {val}")
            ));

            // Usage
            service.AddCell(10); // This will trigger Rule 1 and add 5 automatically.
            service.AddCell(100);
            service.AddCell(103);
            service.AddCell(1000);
            service.AddCell(1001);
            int val = 0;
            foreach (var cell in service.Cells)
            {
                val += cell;
            }
            Console.WriteLine($"Count Cells[{service.Cells.Count()}]; Final value: {val}");
        }
    }
}
