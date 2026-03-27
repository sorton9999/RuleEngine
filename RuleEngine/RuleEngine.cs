using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RuleEngineLib
{
    public class UpdateClass
    {
        private int id;
        private string name;
        public UpdateClass() { }
        public UpdateClass(int id, string name) { this.id = id; this.name = name; }

    }

    public class RuleItemClass
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public bool Update(string name, object value)
        {
            if (this.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                if (Value != value)
                {
                    Value = value;
                    return true;
                }
            }
            return false;
        }
    }

    public abstract class RuleItem
    {
        private string name = String.Empty;
        protected delegate bool UpdateD(string name, object item);
        protected static event UpdateD UpdateItemEvent;

        public string Name { get { return name; } set { name = value; } }

        public static bool? Update(string name, object item)
        {
            return UpdateItemEvent?.Invoke(name, item);
        }
    }

    public class BinaryRuleItem : RuleItem, I_Evaluate
    {
        private object _item1 = null;
        private object _item2 = null;
        private RuleItemClass _ritem1 = null;
        private RuleItemClass _ritem2 = null;
        //Tuple<string, UpdateClass> _class;

        public Func<object, object, bool> Predicate { get; private set; }
        public Func<RuleItemClass, RuleItemClass, bool> RPredicate { get; private set; }
        public object Item1 { get { return _item1; } private set { _item1 = value; } }
        public object Item2 { get { return _item2; } private set { _item2 = value; } }
        public RuleItemClass RItem1 { get { return _ritem1; } private set { _ritem1 = value; } }
        public RuleItemClass RItem2 { get { return _ritem2; } private set { _ritem2 = value; } }

        public bool Evaluate()
        {
            if (Predicate != null)
            {
                return Predicate(Item1, Item2);
            }
            else
            {
                return REvaluate();
            }
        }
        public bool REvaluate()
        {
            return RPredicate(RItem1, RItem2);
        }

        public void UpdatePredicate(Func<object, object, bool> predicate)
        {
            Predicate = predicate;
        }


        public bool Update(string name, object value)
        {
            bool retVal = true;
            if (Predicate == null)
            {
                if (! (bool)RItem1?.Update(name, value))
                {
                    retVal = (bool)RItem2?.Update(name, value);
                }
                retVal = false;
            }
            return retVal;
        }

        public BinaryRuleItem(object item1, object item2, Func<object, object, bool> func, string name = "Dummy")
            : this()
        {
            //_class = new Tuple<string, UpdateClass>(name, new UpdateClass(1, name));
            if (item1 is RuleItemClass)
            {
                RItem1 = item1 as RuleItemClass;
                RItem2 = item2 as RuleItemClass;
                RPredicate = func;
                Name = name;
            }
            else
            {
                Name = name;
                Item1 = item1;
                Item2 = item2;
                Predicate = func;
            }
        }

        public BinaryRuleItem()
        {
            RuleItem.UpdateItemEvent += BinaryRuleItem_UpdateItemEvent;
        }

        private bool BinaryRuleItem_UpdateItemEvent(string name, object item)
        {
            return (bool)Update(name, item);
        }

    }

    public class UnaryRuleItem : RuleItem, I_Evaluate
    {
        private object _item = null;
        private RuleItemClass _itemClass = null;

        public Func<object, bool> Predicate { get; private set; }
        public Func<RuleItemClass, bool> RPredicate { get; private set; }

        public object Item { get { return _item; } private set { _item = value; } }
        public RuleItemClass RItem { get { return _itemClass; } private set { _itemClass = value; } }

        public bool Evaluate()
        {
            if (Predicate == null)
            {
                return RPredicate(RItem);
            }
            else
            {
                return Predicate(Item);
            }
        }

        public bool REvaluate()
        {
            return false;
        }

        public void UpdatePredicate(Func<object, bool> predicate)
        {
            Predicate = predicate;
        }

        public bool Update(string name, object value)
        {
            bool retVal = true;
            if (Predicate == null)
            {
                retVal = (bool)RItem?.Update(name, value);
            }
            else
            {
                _item = value;
            }
            return retVal;
        }


        public UnaryRuleItem(object item, Func<object, bool> func)
            : this()
        {
            if (item is RuleItemClass)
            {
                RItem = item as RuleItemClass;
                RPredicate = func;
            }
            else
            {
                Item = item;
                Predicate = func;
            }
        }

        public UnaryRuleItem()
        {
            RuleItem.UpdateItemEvent += RuleItem_UpdateItemEvent;
        }

        private bool RuleItem_UpdateItemEvent(string name, object item)
        {
            throw new NotImplementedException();
        }

    }

    public class TiedRuleItem : I_Evaluate
    {
        public enum OperationEnum
        {
            None,
            CondAnd,
            CondOr,
            Not,
            LogAnd,
            LogOr,
            Xor
        }

        public TiedRuleItem(I_Evaluate eval1, I_Evaluate eval2, OperationEnum oper)
        {
            Eval1 = eval1;
            Eval2 = eval2;
            PredicateOp = oper;
        }

        public string Name { get; set; }

        public I_Evaluate Eval1 { get; private set; }

        public I_Evaluate Eval2 { get; private set; }

        public OperationEnum PredicateOp { get; private set; }

        public bool Evaluate()
        {
            bool? ans1 = Eval1?.Evaluate();
            bool? ans2 = Eval2?.Evaluate();

            switch (PredicateOp)
            {
                case OperationEnum.CondAnd:
                    //return Eval1.Evaluate() && Eval2.Evaluate();
                    return (bool)ans1 && (bool)ans2;
                case OperationEnum.CondOr:
                    //return Eval1.Evaluate() || Eval2.Evaluate();
                    return (bool)ans1 || (bool)ans2;
                case OperationEnum.Not:
                    return ! (bool)(Eval1?.Evaluate());
                case OperationEnum.Xor:
                    return (bool)(Eval1?.Evaluate() ^ Eval2?.Evaluate());
                case OperationEnum.LogAnd:
                    return (bool)(Eval1?.Evaluate() & Eval2?.Evaluate());
                case OperationEnum.LogOr:
                    return (bool)(Eval1?.Evaluate() | Eval2?.Evaluate());
                case OperationEnum.None:
                default:
                    return false;
            }
        }
        public bool REvaluate()
        {
            bool? ans1 = Eval1?.REvaluate();
            bool? ans2 = Eval2?.REvaluate();

            switch (PredicateOp)
            {
                case OperationEnum.CondAnd:
                    //return Eval1.Evaluate() && Eval2.Evaluate();
                    return (bool)ans1 && (bool)ans2;
                case OperationEnum.CondOr:
                    //return Eval1.Evaluate() || Eval2.Evaluate();
                    return (bool)ans1 || (bool)ans2;
                case OperationEnum.Not:
                    return !(bool)(Eval1?.Evaluate());
                case OperationEnum.Xor:
                    return (bool)(Eval1?.Evaluate() ^ Eval2?.Evaluate());
                case OperationEnum.LogAnd:
                    return (bool)(Eval1?.Evaluate() & Eval2?.Evaluate());
                case OperationEnum.LogOr:
                    return (bool)(Eval1?.Evaluate() | Eval2?.Evaluate());
                case OperationEnum.None:
                default:
                    return false;
            }
        }

        public bool Update(string name, object value)
        {
            bool? ans1 = Eval1?.Update(name, value);
            bool? ans2 = Eval2?.Update(name, value);
            return (bool)(ans1 & ans2);
        }
    }

    public class RuleEngine : I_RuleEvaluation
    {
        /// <summary>
        /// Provides a storage area of multiple lists of rulesets.  The idea is to preload the rules
        /// that different operations have criteria for and access them by name and evaluate.
        /// </summary>
        private readonly Dictionary<string, List<I_Evaluate>> multipleRuleSets = new Dictionary<string, List<I_Evaluate>>();
        
        /// <summary>
        /// The list of rules to evaluate.  This needs to be filled in from the MultipleRuleSets storage
        /// before evaluation.
        /// </summary>
        private List<I_Evaluate> rules = new List<I_Evaluate>();

        #region Properties

        /// <summary>
        /// Returns the count of the loaded rules in the rules list
        /// </summary>
        public int RuleCount
        { 
            get { return rules.Count; }
        }

        /// <summary>
        /// Returns the count of the number of loaded rulesets.
        /// </summary>
        public int RuleDictCount
        {
            get { return multipleRuleSets.Count; }
        }

        /// <summary>
        /// Returns a list of rulesets names as stored in the multiple rules container.
        /// </summary>
        public string[] RuleNames
        {
            get { return multipleRuleSets.Keys.ToArray(); }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Clears the rules list.
        /// </summary>
        public void ClearRules()
        {
            rules.Clear();
        }

        /// <summary>
        /// Clears the rules list as well as the multiple rules container.
        /// </summary>
        public void ClearAllRules()
        {
            rules.Clear();
            multipleRuleSets.Clear();
        }

        public bool UpdateBinaryPredicate(string rsName, string ruleName, Func<object, object, bool> predicate)
        {
            bool retVal = false;
            if (String.IsNullOrEmpty(rsName) || String.IsNullOrEmpty(ruleName) || (predicate == null))
            {
                return false;
            }
            if (multipleRuleSets.TryGetValue(rsName, out List<I_Evaluate> results))
            {
                I_Evaluate rule = results.Find((l) => l.Name == ruleName);
                if (rule != null && rule is BinaryRuleItem item)
                {
                    item.UpdatePredicate(predicate);
                    retVal = true;
                }
            }
            return retVal;
        }

        public bool UpdateUnaryPredicate(string rsName, string ruleName, Func<object, bool> predicate)
        {
            bool retVal = false;
            if (String.IsNullOrEmpty(rsName) || String.IsNullOrEmpty(ruleName) || (predicate == null))
            {
                return false;
            }
            if (multipleRuleSets.TryGetValue(rsName, out List<I_Evaluate> results))
            {
                I_Evaluate rule = results.Find((l) => l.Name == ruleName);
                if (rule != null && rule is UnaryRuleItem item)
                {
                    item.UpdatePredicate(predicate);
                    retVal = true;
                }
            }
            return retVal;
        }

        /// <summary>
        /// The main call to evaluate all the rules stored in the rules list.
        /// This is a logical AND operation so the evaluation continues until
        /// a FALSE is obtained whereby it will return FALSE.  Otherwise it
        /// will return TRUE.
        /// </summary>
        /// <returns>Whether or not all the evaluations come out TRUE</returns>
        public bool Evaluate()
        {
            bool retVal = true;
            foreach (var rule in rules)
            {
                // Short circuit return on the first FALSE
                if (!rule.Evaluate()) { return false; }
            }
            return retVal;
        }

        /// <summary>
        /// Evaluate the named ruleset.  The class rules list is used.  It is assumed
        /// all rules are in place when the evaluation is called for.  This is a
        /// stepwise logical AND operation.
        /// </summary>
        /// <param name="name">The ruleset to evaluate</param>
        /// <returns>The result of the Evaluate operation</returns>
        public bool Evaluate(string name)
        {
            rules.Clear();
            if (multipleRuleSets.TryGetValue(name, out rules))
            {
                bool ans = Evaluate();
                //return Evaluate();
                return ans;
            }
            return false;
        }

        /// <summary>
        /// The main call to evaluate all the rules stored in the rules list.
        /// This is a logical OR operation where all the rules are evaluated
        /// in order and the final answer is returned.
        /// </summary>
        /// <returns>Whether or not the operation is successful</returns>
        public bool EvaluateOr()
        {
            bool retVal = true;
            foreach (var rule in rules)
            {
                retVal |= rule.Evaluate();
            }
            return retVal;
        }

        /// <summary>
        /// Evaluate the named ruleset.  The class rules list is used.  It is assumed
        /// all rules are in place when the evaluation is called for.  This is a
        /// stepwise logical OR operation.
        /// </summary>
        /// <param name="name">The ruleset to evaluate</param>
        /// <returns>The result of the Evaluate operation</returns>
        public bool EvaluateOr(string name)
        {
            rules.Clear();
            if (multipleRuleSets.TryGetValue(name, out rules))
            {
                return EvaluateOr();
            }
            return false;
        }

        /// <summary>
        /// Adds a rule of type I_Evaluate to the rules list.  There is no verification
        /// for duplicates done.
        /// </summary>
        /// <param name="item">The rule to add</param>
        public void AddRule(I_Evaluate item)
        {
            rules.Add(item);
        }

        /// <summary>
        /// Adds the loaded ruleset with the given name string as the key.  It takes the
        /// rules list that has been previously loaded and adds it to the multiple ruleset
        /// container.
        /// The rules list is then cleared so it can be reloaded.  If the flag 'clearRules'
        /// is set to FALSE, the rules list is not cleared and can be reloaded into another
        /// named ruleset in a separate call to this method.
        /// </summary>
        /// <param name="name">The key used to store the previously loaded rules list</param>
        /// <returns>Whether or not the operation is successful</returns>
        public bool AddLoadedRuleset(string name, bool clearRules = true)
        {
            bool retVal = true;
            if (rules.Count <= 0)
            {
                return false;
            }
            try
            {
                I_Evaluate[] evals = new I_Evaluate[rules.Count];
                rules.CopyTo(evals);
                multipleRuleSets.Add(name, evals.ToList());
                if (clearRules)
                {
                    ClearRules();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AddNamedRules Exception: " + ex.Message);
                retVal = false;
            }
            return retVal;
        }

        /// <summary>
        /// Adds a ruleset to the multiple ruleset container.
        /// </summary>
        /// <param name="name">The name to give the ruleset to add</param>
        /// <param name="rules">The ruleset to add</param>
        /// <returns>Whether or not the operation is successful</returns>
        public bool AddRuleset(string name, List<I_Evaluate> rules)
        {
            bool retval = false;
            if ( rules.Count <= 0 )
            {
                return false;
            }
            try
            {
                multipleRuleSets.Add(name, rules);
                retval = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AddRuleset Exception: " + ex.Message);
            }
            return retval;
        }

        /// <summary>
        /// Adds a rule to the named ruleset.
        /// </summary>
        /// <param name="name">The name of the ruleset to add to</param>
        /// <param name="rule">The rule to add</param>
        /// <returns>Whether or not the operation is successful</returns>
        public bool AddToNamedRules(string name, I_Evaluate rule)
        {
            bool retVal = false;
            try
            {
                if (multipleRuleSets.TryGetValue(name, out List<I_Evaluate> list))
                {
                    list.Add(rule);
                    retVal = true;
                }
                else
                {
                    List<I_Evaluate> newList = new List<I_Evaluate>();
                    newList.Add(rule);
                    multipleRuleSets.Add(name, newList);
                    retVal = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AddToNamedRules Exception: " + ex.Message);
            }
            return retVal;
        }

        /// <summary>
        /// A convenience method to add an Equality rule to the rules list.  It creates a BinaryRuleItem
        /// with the 2 given items as arguments along with the equality predicate.
        /// </summary>
        /// <param name="item1">The first comparison item</param>
        /// <param name="item2">The second comparison item<</param>
        public void AddEquality(object item1, object item2)
        {
            rules.Add(new BinaryRuleItem(item1, item2, (a, b) => { return a.Equals(b); }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item1"></param>
        /// <param name="item2"></param>
        public void AddEquality(RuleItemClass item1, RuleItemClass item2)
        {
            rules.Add(new BinaryRuleItem(item1, item2, (a, b) => { return a.Equals(b); }));
        }

        /// <summary>
        /// A conveniance method to add an Equality rule to the multiple ruleset container using the
        /// given key.  If the key is not in the container and the flag 'addNew' is TRUE, the given
        /// items are added as a new entry into the container with the given key.
        /// </summary>
        /// <param name="name">The key to store the rule with</param>
        /// <param name="item1">The first comparison item</param>
        /// <param name="item2">The second comparison item<</param>
        /// <returns></returns>
        public bool AddEqualityToRuleset(string name, object item1, object item2, bool addNew = false)
        {
            bool retVal = false;
            try
            {
                if (multipleRuleSets.TryGetValue(name, out List<I_Evaluate> list))
                {
                    list.Add(new BinaryRuleItem(item1, item2, (a, b) => { return a.Equals(b); }));
                    retVal = true;
                }
                else if (addNew)
                {
                    List<I_Evaluate> newList = new List<I_Evaluate>
                    {
                        new BinaryRuleItem(item1, item2, (a, b) => { return a.Equals(b); })
                    };
                    multipleRuleSets.Add(name, newList);
                    retVal = true;
                    System.Diagnostics.Debug.WriteLine("Adding new ruleset with the given key: " + name);
                }
            }
            catch (Exception ex)
            {
                if (addNew)
                {
                    List<I_Evaluate> list = new List<I_Evaluate>
                    {
                        new BinaryRuleItem(item1, item2, (a, b) => { return a.Equals(b); })
                    };
                    multipleRuleSets.Add(name, list);
                    retVal = true;
                }
                System.Diagnostics.Debug.WriteLine("AddEqualityToRuleset Exception: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Adding new ruleset with the given key: " + name);
            }
            return retVal;
        }

        /// <summary>
        /// A convenience method to add an Inequality rule to the rules list.  It creates a BinaryRuleItem
        /// with the 2 given items as arguments along with the inequality predicate.
        /// </summary>
        /// <param name="item1">The first comparison item</param>
        /// <param name="item2">The second comparison item</param>
        public void AddInequality(object item1, object item2)
        {
            rules.Add(new BinaryRuleItem(item1, item2, (a, b) => { return ! a.Equals(b); }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item1"></param>
        /// <param name="item2"></param>
        /// <param name="name"></param>
        public void AddInequalityRuleItem(RuleItemClass item1, RuleItemClass item2, string name)
        {
            rules.Add(new BinaryRuleItem(item1, item2, (a, b) => { return ! a.Equals(b); }, name));
        }

        public bool Update(string ruleName, int ruleIndex, string name, object value)
        {
            bool retVal = false;
            if (multipleRuleSets.TryGetValue(ruleName, out List<I_Evaluate> lRules))
            {
                retVal = lRules[ruleIndex].Update(name, value);
            }
            return retVal;
        }

        /// <summary>
        /// A conveniance method to add an Inequality rule to the multiple ruleset container using the
        /// given key.  If the key is not in the container and the flag 'addNew' is TRUE, the given
        /// items are added as a new entry into the container with the given key.
        /// </summary>
        /// <param name="name">The key to store the rule with</param>
        /// <param name="item1">The first comparison item</param>
        /// <param name="item2">The second comparison item</param>
        /// <returns></returns>
        public bool AddInequalityToRuleset(string name, object item1, object item2, bool addNew = false)
        {
            bool retVal = false;
            try
            {
                if (multipleRuleSets.TryGetValue(name, out List<I_Evaluate> list))
                {
                    list.Add(new BinaryRuleItem(item1, item2, (a, b) => { return ! a.Equals(b); }));
                    retVal = true;
                }
                else if (addNew)
                {
                    List<I_Evaluate> newList = new List<I_Evaluate>
                    {
                        new BinaryRuleItem(item1, item2, (a, b) => { return ! a.Equals(b); })
                    };
                    multipleRuleSets.Add(name, newList);
                    retVal = true;
                    System.Diagnostics.Debug.WriteLine("Adding new ruleset with the given key: " + name);
                }
            }
            catch (Exception ex)
            {
                if (addNew)
                {
                    List<I_Evaluate> list = new List<I_Evaluate>
                    {
                        new BinaryRuleItem(item1, item2, (a, b) => { return ! a.Equals(b); })
                    };
                    multipleRuleSets.Add(name, list);
                    retVal = true;
                }
                System.Diagnostics.Debug.WriteLine("AddInequalityToRuleset Exception: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Adding new ruleset with the given key: " + name);
            }
            return retVal;
        }


        #endregion

    }
}
