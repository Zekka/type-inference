using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace TypeSystem
{
    internal class Vars
    {
        private List<string> _trace;
        public IEnumerable<string> Trace => _trace;
        private FunctionalDependencies _constraintFundeps;
        private List<term> _constraints;

        private Dictionary<atom, term> _bindings;

        public Vars(FunctionalDependencies constraintFundeps)
        {
            _trace = new List<string>();
            _constraintFundeps = constraintFundeps;
            _constraints = new List<term>();
            _bindings = new Dictionary<atom, term>();
        }

        private Vars(
            List<string> trace,
            FunctionalDependencies cfundeps,
            List<term> constraints,
            Dictionary<atom, term> bindings
        )
        {
            _trace = trace;
            _constraintFundeps = cfundeps;
            _constraints = constraints;
            _bindings = bindings;
        }

        private Vars Duplicate()
        {
            return new Vars(
                _trace.ToList(),
                _constraintFundeps,
                _constraints.ToList(),
                new Dictionary<atom, term>(_bindings)
            );
        }

        // TODO: Don't keep detailed traces if we're unifying and don't produce the message
        public bool Failed => _trace.Any();
        private void FailMessage(Func<string> message) 
        {
            _trace.Add(message());
        }

        public bool Unify(type t1, type t2)
        {
            if (Failed) { throw new Exception("this Vars failed and is in an invalid state"); }

            foreach (var c in t1.Constraints)
            {
                Constrain(c);
                if (_trace.Any())
                {
                    FailMessage(() => $"while adding constraint {c} from {t1}");
                    return false;
                }
            }

            foreach (var c in t2.Constraints)
            {
                Constrain(c);
                if (_trace.Any())
                {
                    FailMessage(() => $"while adding constraint {c} from {t2}");
                    return false;
                }
            }

            Unify(t1.Term, t2.Term);
            if (Failed)
            {
                FailMessage(() => $"while unifying type {t1} with {t2}");
                return false;
            }

            return true;
        }

        public bool Constrain(term constraint)
        {
            var key = _constraintFundeps.Key(constraint);
            var constraints2 = new List<term>();

            foreach (var existingConstraint in _constraints)
            {
                var theirKey = _constraintFundeps.Key(constraint);
                var decoy = this.Duplicate(); // create a savestate in case this fails
                if (decoy.Unify(key, theirKey))
                {
                    Unify(key, theirKey); // commit to this instance
                    if (Failed)
                    {
                        throw new Exception(
                            "failure was supposed to be impossible because we used a decoy, but it happened anyways");
                    }

                    // the terms have matching keys so now
                    // we have to be able to unify the terms
                    Unify(constraint, existingConstraint);
                    if (Failed)
                    {
                        FailMessage(() =>
                            $"while unifying new constraint {constraint} with existing matching-fundep constraint {existingConstraint}");
                        return false;
                    }
                }
                else
                {
                    constraints2.Add(existingConstraint); // pass through unchanged
                }
            }

            _constraints = constraints2;

            return true;
        }

        public bool Unify(term t1, term t2)
        {
            if (Failed) { throw new Exception("this Vars failed and is in an invalid state"); }
            switch (t1.Kind)
            {
                case TermKind.Atom:
                    switch (t2.Kind)
                    {
                        case TermKind.Atom:
                            if (t1.AtomAtom != t2.AtomAtom)
                            {
                                FailMessage(() => $"mismatching atoms: {t1.AtomAtom} and {t2.AtomAtom}");
                                return false;
                            }
                            return true;
                        case TermKind.Apply:
                            FailMessage(() => $"could not match atom {t1.AtomAtom} with application {t2}");
                            return false;
                        case TermKind.Var: return Unify(t2, t1); // only t1 may be a var unless both are
                    }
                    throw new Exception("unreachable case");
                case TermKind.Apply:
                    switch (t2.Kind)
                    {
                        case TermKind.Atom:
                            FailMessage(() => $"could not match application {t1} with atom {t2.AtomAtom}");
                            return false;
                        case TermKind.Apply:
                            Unify(t1.ApplyF, t2.ApplyF);
                            if (Failed)
                            {
                                FailMessage(() => $"while unifying term {t1} with {t2}");
                                return false;
                            }
                            Unify(t1.ApplyX, t2.ApplyX);
                            if (Failed)
                            {
                                FailMessage(() => $"while unifying term {t1} with {t2}");
                                return false;
                            }
                            return true;
                        case TermKind.Var: return Unify(t2, t1); // only t1 may be a var unless both are
                    }
                    throw new Exception("unreachable case");
                case TermKind.Var:
                    term bound;
                    OccursCheck(t1.VarName, t2);
                    if (Failed) {
                        FailMessage(() => $"{t1.VarName} occurs in {t2}, so it can't match with it");
                        FailMessage(() => $"while unifying term {t1} with {t2}");
                        return false;
                    }
                    if (_bindings.TryGetValue(t1.VarName, out bound))
                    {
                        Unify(bound, t2);
                        if (Failed)
                        {
                            FailMessage(() => $"while unifying term {t1} with {t2}");
                            return false;
                        }
                        return true;
                    }
                    // unifying passes trivial
                    _bindings[t1.VarName] = t2;
                    return true;
            }
            throw new Exception("unreachable case");
        }

        // true: occurs check passed (meaning var doesn't occur)
        // doesn't produce output beyond "occurs check failed" to avoid generating loads of garbage
        // *except when it follows a variable*
        private bool OccursCheck(atom var, term t2)
        { 
            if (Failed) { throw new Exception("this Vars failed and is in an invalid state"); }
            switch (t2.Kind) {
                case TermKind.Atom:
                    return true;
                case TermKind.Apply:
                    OccursCheck(var, t2.ApplyF);
                    if (Failed) { return false; }
                    OccursCheck(var, t2.ApplyX);
                    if (Failed) { return false; }
                    return true;
                case TermKind.Var:
                    if (var == t2.VarName)
                    {
                        FailMessage(() => "occurs check failed");
                        return false;
                    }
                    term bound;
                    if (_bindings.TryGetValue(t2.VarName, out bound))
                    {
                        OccursCheck(var, bound);
                        FailMessage(() => $"when resolving {t2.VarName} to {bound}");
                        return false;
                    }
                    return true;
            }

            throw new Exception("unreachable case");
        }

        public type InstantiateWithConstraints(term term)
        {
            if (Failed) { throw new Exception("this Vars failed and is in an invalid state"); }

            var termBase = InstantiateWithoutConstraints(term);
            var variables = termBase.Variables();
            var salientConstraints = new List<term>();

            foreach (var c in _constraints)
            {
                var instantiated = InstantiateWithoutConstraints(c);
                if (instantiated.Variables().Intersect(variables).Any())
                {
                    salientConstraints.Add(c);
                }
            }

            return new type(salientConstraints, termBase);
        }

        public term InstantiateWithoutConstraints(term term)
        {
            if (Failed) { throw new Exception("this Vars failed and is in an invalid state"); }

            switch (term.Kind)
            {
                case TermKind.Atom: return term;
                case TermKind.Apply:
                    return term.Apply(InstantiateWithoutConstraints(term.ApplyF),
                        InstantiateWithoutConstraints(term.ApplyX));
                case TermKind.Var:
                    term value;
                    if (_bindings.TryGetValue(term.VarName, out value)) { return InstantiateWithoutConstraints(value); }
                    return term;
            }
            throw new Exception("unreachable code");
        }
    }

    // Functional dependencies determine how you merge constraints that appear to overlap.
    //
    // As an example, you might have a constraint that looks like this: CanConvert Int Double
    // That's not mutually exclusive with CanConvert Int Float. 
    //
    // However, you might have RepresentedAs Int Bit64 and RepresentedAs Int Bit32. Those
    // certainly would conflict!
    //
    // Basically, we encode this knowledge by saying that all constraints with keys that unify 
    // with the new constraint's FunctionalDependencies.Key must unify with the new constraint.
    internal class FunctionalDependencies
    {
        private List<Func<term, term?>> _keyFinders; // each keyFinder may return null
        public FunctionalDependencies()
        {
            _keyFinders = new List<Func<term, term?>>();
        }

        public term Key(term constraint)
        {
            foreach (var k in _keyFinders)
            {
                var key = k(constraint);
                if (key.HasValue) { return key.Value; }
            }
            return constraint; // by default, overlapping constraints are fine
        }
    }
}