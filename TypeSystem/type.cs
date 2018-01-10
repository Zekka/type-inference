using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace TypeSystem
{
    internal static class types
    {
        public readonly static free<type> MostGeneral = free.Of(type.Var(new atom("any")));
    }

    internal static class free
    {
        public static free<T> Of<T>(T t)
        {
            return new free<T>(t);
        }
    }

    internal struct free<T>
    {
        public readonly T UnsafeValue;

        public free(T t)
        {
            if (t == null)
            {
                throw new Exception("can't be null");
            }
            UnsafeValue = t;
        }

        public override string ToString()
        {
            return $"free({UnsafeValue})";
        }
    }

    internal static class FreeExtensions
    {
        public static bool Equivalent(this free<type> type1, free<type> type2)
        {
            var t1 = type1.Canonicalize();
            var t2 = type2.Canonicalize();

            return t1.UnsafeValue == t2.UnsafeValue;
        }

        public static bool Equivalent(this free<term> term1, free<term> term2)
        {
            var t1 = term1.Canonicalize();
            var t2 = term2.Canonicalize();

            return t1.UnsafeValue == t2.UnsafeValue;
        }

        public static free<type> Canonicalize(this free<type> type)
        {
            return free.Of(new Canonicalizer().Canonicalize(type.UnsafeValue));
        }

        public static free<term> Canonicalize(this free<term> term)
        {
            return free.Of(new Canonicalizer().Canonicalize(term.UnsafeValue));
            
        }


        private class Canonicalizer
        {
            private Dictionary<atom, atom> _canonicalName;
            private int _counter;

            internal Canonicalizer()
            {
                _canonicalName = new Dictionary<atom, atom>();
                _counter = 0;
            }

            internal type Canonicalize(type type)
            {
                return new type(from c in type.Constraints select Canonicalize(c), Canonicalize(type.Term));
            }

            internal term Canonicalize(term term)
            {
                switch (term.Kind)
                {
                    case TermKind.Atom: return term;
                    case TermKind.Apply: return term.Apply(Canonicalize(term.ApplyF), Canonicalize(term.ApplyX));
                    case TermKind.Var:
                        atom existing;
                        if (_canonicalName.TryGetValue(term.VarName, out existing))
                        {
                            return term.Var(existing);
                        }
                        return term.Var(_canonicalName[term.VarName] = new atom("var_" + (++_counter)));
                }
                throw new Exception("unreachable path");
            }
        }
    }


    internal class type
    {
        private readonly term[] _constraints;
        public IEnumerable<term> Constraints => _constraints;

        public readonly term Term;

        public type(IEnumerable<term> constraints, term term)
        {
            _constraints = constraints.ToArray();
            Array.Sort(_constraints, new ConstraintComparer());
            Term = term;
        }

        protected bool Equals(type other)
        {
            return _constraints.SequenceEqual(other._constraints) && Term.Equals(other.Term);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((type) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (DJB2.HashIEnumerable(_constraints) * 397) ^ Term.GetHashCode();
            }
        }

        public static bool operator ==(type left, type right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(type left, type right)
        {
            return !Equals(left, right);
        }

        public static type Atom(atom a) => new type(new term[0], term.Atom(a));
        public static type Apply(term a, term b) => new type(new term[0], term.Apply(a, b));
        public static type Var(atom a) => new type(new term[0], term.Var(a));

        public override string ToString()
        {
            if (Constraints.Any())
            {
                return string.Join(", ", from c in Constraints select c.ToString()) +" => " + Term;
            }
            return "true => " + Term;
        }
    }

    // There's not a correct sort order for terms, but this makes the order consistent
    struct ConstraintComparer : IComparer<term>
    {
        public int Compare(term x, term y)
        {
            switch (x.Kind)
            {
                case TermKind.Atom:
                    switch (y.Kind)
                    {
                        case TermKind.Atom:
                            return x.AtomAtom.Value.CompareTo(y.AtomAtom);
                        case TermKind.Apply:
                            return -1;
                        case TermKind.Var:
                            return -1;
                    }
                    throw new Exception("unreachable case");
                case TermKind.Apply:
                    switch (y.Kind)
                    {
                        case TermKind.Atom:
                            return 1;
                        case TermKind.Apply:
                            int c;
                            if ((c = Compare(x.ApplyF, y.ApplyF)) != 0) { return c; }
                            if ((c = Compare(x.ApplyX, y.ApplyX)) != 0) { return c; }
                            return 0;
                        case TermKind.Var:
                            return -1;
                    }
                    throw new Exception("unreachable case");
                case TermKind.Var:
                    switch (y.Kind)
                    {
                        case TermKind.Atom:
                            return 1;
                        case TermKind.Apply:
                            return 1;
                        case TermKind.Var:
                            return x.VarName.Value.CompareTo(y.VarName);
                    }
                    throw new Exception("unreachable case");
            }
            throw new Exception("unreachable case");
        }
    }

    // VALUES:
    // Atom(atom)
    // Apply(term, term)
    // Var(atom)
    struct term
    {
        public readonly TermKind Kind;

        private readonly atom _atom;

        private readonly Box<term> _applyF;
        private readonly Box<term> _applyX;

        public static term Atom(atom a) => new term(TermKind.Atom, a);
        public static term Apply(term t1, term t2) => new term(t1, t2);
        public static term Var(atom a) => new term(TermKind.Var, a);

        private term(TermKind kind, atom a)
        {
            Kind = kind;
            _atom = a;
            _applyF = new Box<term>(default(term));
            _applyX = new Box<term>(default(term));
        }

        private term(term t1, term t2)
        {
            Kind = TermKind.Apply;
            _atom = default(atom);
            _applyF = new Box<term>(t1);
            _applyX = new Box<term>(t2);
        }

        public atom AtomAtom
        {
            get
            {
                if (Kind != TermKind.Atom) { throw new Exception("can't destruct a non-atom as an atom"); }
                return _atom;
            }
        }

        public term ApplyF
        {
            get
            {
                if (Kind != TermKind.Apply) { throw new Exception("can't destruct a non-apply as an apply"); }
                return _applyF.Value;
            }
        }

        public term ApplyX
        {
            get
            {
                if (Kind != TermKind.Apply) { throw new Exception("can't destruct a non-apply as an apply"); }
                return _applyX.Value;
            }
        }

        public atom VarName
        {
            get
            {
                if (Kind != TermKind.Var) { throw new Exception("can't destruct a non-var as an var"); }
                return _atom;
            }
        }

        public bool Equals(term other)
        {
            if (Kind != other.Kind) { return false; }
            switch (Kind)
            {
                case TermKind.Atom: return AtomAtom == other.AtomAtom;
                case TermKind.Apply: return ApplyF == other.ApplyF && ApplyX == other.ApplyX;
                case TermKind.Var: return VarName == other.VarName;
            }
            throw new Exception("unreachable code path");
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is term && Equals((term) obj);
        }

        public override int GetHashCode()
        {
            switch (Kind)
            {
                case TermKind.Atom: return DJB2.Hash(Kind, AtomAtom);
                case TermKind.Apply: return DJB2.Hash(Kind, ApplyF, ApplyX);
                case TermKind.Var: return DJB2.Hash(Kind, VarName);
            }
            throw new Exception("unreachable code path");
        }

        public static bool operator ==(term left, term right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(term left, term right)
        {
            return !left.Equals(right);
        }

        public IEnumerable<atom> Variables()
        {
            switch (Kind)
            {
                case TermKind.Atom: yield break;
                case TermKind.Apply:
                    foreach (var i in this.ApplyF.Variables()) { yield return i; }
                    foreach (var i in this.ApplyX.Variables()) { yield return i; }
                    yield break;
                case TermKind.Var:
                    yield return VarName;
                    yield break;
            }
            throw new Exception("unreachable code");
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case TermKind.Atom: return $"{this.AtomAtom}";
                case TermKind.Apply: return $"{this.ApplyF} ({this.ApplyX})";
                case TermKind.Var: return $"@{this.VarName}";
            }
            throw new Exception("unreachable code");
        }
    }

    enum TermKind
    {
        Atom,
        Apply,
        Var
    }
}