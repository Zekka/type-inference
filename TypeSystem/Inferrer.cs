using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeSystem
{
    class Inferrer<ClueSource, ClueConstant>
    {
        private readonly SymbolGenerator _clueIdGen;
        private readonly FunctionalDependencies _constraintFundeps;
         
        private readonly Dictionary<ClueId, atom> _clueKind;
        private readonly Dictionary<ClueId, ClueSource> _clueSource;
        private readonly Dictionary<ClueId, ClueConstant> _clueConstant;
        private readonly Dictionary<ClueId, ClueId[]> _clueDependsOn;
        private readonly Dictionary<ClueId, HashSet<ClueId>> _cluesDependingOn;
        private readonly Dictionary<ClueId, free<type>> _clueBestKnownType;
        private readonly Dictionary<ClueId, List<InferenceError>> _clueErrors;
        private readonly SortedSet<ClueId> _cluesToUpdate;

        private readonly List<InferenceRule<ClueConstant>> _rules;

        private class InferenceError
        {
            internal readonly string Message;
            internal readonly ClueSource[] Culpable;

            internal InferenceError(string message, params ClueSource[] culpable)
            {
                Message = message;
                Culpable = culpable.ToArray();
            }
        }

        // OPTIMIZATION: Use heap. 
        // We use a sortedset instead of a queue because lower clue IDs can't depend on greater clue IDs
        // This handily avoids Painter's Algorithm issues *and* deduplicates IDs! Magic.

        public Inferrer(FunctionalDependencies fundeps)
        {
            _clueIdGen = new SymbolGenerator();
            _constraintFundeps = fundeps;

            _clueKind = new Dictionary<ClueId, atom>();
            _clueSource = new Dictionary<ClueId, ClueSource>();
            _clueConstant = new Dictionary<ClueId, ClueConstant>();
            _clueDependsOn = new Dictionary<ClueId, ClueId[]>();
            _cluesDependingOn = new Dictionary<ClueId, HashSet<ClueId>>();
            _clueBestKnownType = new Dictionary<ClueId, free<type>>();
            _clueErrors = new Dictionary<ClueId, List<InferenceError>>();
            _cluesToUpdate = new SortedSet<ClueId>(new ClueIdComparer());

            _rules = new List<InferenceRule<ClueConstant>>();
        }

        public void CreateRule(InferenceRule<ClueConstant> ir)
        {
            _rules.Add(ir);
        }

        public ClueId CreateClue(atom kind, ClueSource source, ClueConstant constant, params ClueId[] dependsOn)
        {
            foreach (var other in dependsOn)
            {
                if (other == null) { throw new Exception("null is not a clue that you can depend on"); }
                if (!_clueKind.ContainsKey(other)) { throw new Exception("can't depend on a clue that doesn't exist"); }
            }

            var id = _clueIdGen.GenerateClueId(kind);

            _clueKind[id] = kind;
            _clueSource[id] = source;
            _clueConstant[id] = constant;
            _clueDependsOn[id] = dependsOn.ToArray();

            foreach (var other in dependsOn)
            {
                _cluesDependingOn[other].Add(id);
            }
            _cluesDependingOn[id] = new HashSet<ClueId>();
            _clueBestKnownType[id] = types.MostGeneral;
            _clueErrors[id] = new List<InferenceError>();
            _cluesToUpdate.Add(id);

            return id;
        }

        public void Process()
        {
            while (_cluesToUpdate.Any())
            {
                var next = _cluesToUpdate.Min;
                _cluesToUpdate.Remove(next);

                RunInferenceRulesOn(next);
            }
        }

        private void RunInferenceRulesOn(ClueId next)
        {
            if (_clueErrors[next].Any())
            {
                // If it errored out, we'll only be making it worse
                return;
            }

            var targets = (new[] { next }.Concat(_clueDependsOn[next])).ToArray(); // 0 is always the next, 1 and on are its dependencies
            foreach (var t in targets)
            {
                if (_clueErrors[t].Any())
                {
                    // TODO: We can't really deduce anything
                    _clueErrors[next].Add(new InferenceError($"depended-on clue had an error: {t}"));
                }
            }

            if (_clueErrors[next].Any())
            {
                // If it errored out, we'll only be making it worse
                return;
            }

            foreach (var rule in _rules)
            {
                var tools = new Tools(this, targets);
                rule(tools);
                if (tools.Error != null)
                {
                    if (tools.PreconditionsSatisfied)
                    {
                        _clueErrors[next].Add(tools.Error); // if one inference rule failed, others surely will too!
                        return;
                    }
                    // it's fine, that rule just doesn't apply here apparently
                    continue;
                }

                for (var i = 0; i < targets.Length; i++)
                {
                    var clue = targets[i];
                    var oldType = _clueBestKnownType[clue];
                    var newType = free.Of(tools.Instantiate(i));
                    if (oldType.Equivalent(newType)) { continue; }

                    // if we're updating to a genuinely new type, rerun rules depending on us
                    _clueBestKnownType[clue] = newType;
                    foreach (var depender in _cluesDependingOn[clue])
                    {
                        _cluesToUpdate.Add(depender);
                    }
                }
            }
        }

        class Tools: IInferenceTools<ClueConstant>
        {
            private readonly Inferrer<ClueSource, ClueConstant> _inferrer;
            private ClueId[] _clues;
            private readonly Vars _vars;
            private SymbolGenerator _generator;
            private Scope _ruleScope;
            private Scope[] _userScopes;
            internal bool PreconditionsSatisfied { get; private set; } // when this is true, treat errors as actual errors, not just indicators that the rule can't apply here
            private bool Failed => Error != null;

            internal InferenceError Error { get; private set; }

            internal Tools(Inferrer<ClueSource, ClueConstant> inferrer, params ClueId[] clues)
            {
                _inferrer = inferrer;
                _clues = clues.ToArray(); 
                _vars = new Vars(inferrer._constraintFundeps);
                _generator = new SymbolGenerator();
                _ruleScope = _generator.CreateScope();
                _userScopes = (from _ in clues select _generator.CreateScope()).ToArray();
                PreconditionsSatisfied = false;
                Error = null;
            }

            private InferenceError GenerateError(string error, IEnumerable<ClueSource> involved)
            {
                var trace = string.Join("\n", _vars.Trace.Reverse().ToList());

                return new InferenceError(error == null ? trace : $"{error}\n\n{trace}".Trim(), involved.ToArray());
            }

            // TODO: Bounds check
            public atom Kind(int i)
            {
                return _inferrer._clueKind[_clues[i]];
            }

            // TODO: Bounds check
            public ClueConstant Constant(int i)
            {
                return _inferrer._clueConstant[_clues[i]];
            }

            // TODO: Bounds check
            private ClueSource Source(int i)
            {
                return _inferrer._clueSource[_clues[i]];
            }

            // TODO: Bounds check
            private free<type> Type(int i)
            {
                return _inferrer._clueBestKnownType[_clues[i]];
            }

            public void Fail(string msg, params int[] blame)
            {
                if (Failed) { return; }
                Error = GenerateError(msg, from i in blame select Source(i));
            }

            public void PreconditionsAreSatisfied()
            {
                PreconditionsSatisfied = true;
            }

            public void Unify(free<type> t, int i)
            {
                if (Failed) { return; }
                _vars.Unify(Rule(t), User(i, Type(i)));
                if (_vars.Failed) { Error = GenerateError($"could not match {i} with inference rule type {t}", new[] {Source(i)}); }
            }

            public void Unify(int i1, int i2)
            {
                if (Failed) { return; }
                _vars.Unify(User(i1, Type(i1)), User(i2, Type(i2)));
                if (_vars.Failed) { Error = GenerateError($"could not match {i1} with {i2}", new[] {Source(i1), Source(i2)}); }
            }

            private type Rule(free<type> free) => _ruleScope.Unfree(free);
            private type User(int i, free<type> free) => _userScopes[i].Unfree(free);

            internal type Instantiate(int i)
            {
                return _vars.InstantiateWithConstraints(User(i, Type(i)).Term);
            }
        }

        public free<type> Type(ClueId cval)
        {
            return _clueBestKnownType[cval];
        }

        public IEnumerable<string> Errors(ClueId c)
        {
            return from e in _clueErrors[c] select e.Message; // TODO: Include source
        }
    }

    delegate void InferenceRule<ClueConstant>(IInferenceTools<ClueConstant> tools);

    interface IInferenceTools<ClueConstant>
    {
        atom Kind(int i);
        ClueConstant Constant(int i);
        void Fail(string msg, params int[] blame);
        void Unify(free<type> t, int i); // NOTE: all free types provided through this constructor are bound in the same scope, belonging to the tools
        void Unify(int i1, int i2);

        void PreconditionsAreSatisfied();
    }


    internal class ClueIdComparer : IComparer<ClueId>
    {
        // TODO: Handle nulls somehow
        public int Compare(ClueId x, ClueId y) => x.AbsoluteNumber.CompareTo(y.AbsoluteNumber);
    }
}
