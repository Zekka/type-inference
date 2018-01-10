using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeSystem
{
    class SymbolGenerator
    {
        // keeping track of count for each string means we generate small numbers more often, making stuff easier to understand
        private Dictionary<atom, int> _counter;

        private int _absolute;

        public SymbolGenerator()
        {
            _counter = new Dictionary<atom, int>();
            _absolute = 0;
        }

        public Scope CreateScope()
        {
            return new Scope(this);
        }

        public A Generate<A>(atom prefix, Func<atom, int, int, A> build)
        {
            int count;
            if (!_counter.TryGetValue(prefix, out count)) { count = 0; }
            return build(prefix, _counter[prefix] = ++count, ++_absolute);
        }

        public ClueId GenerateClueId(atom prefix) => 
            Generate(prefix, (p, n, abs) => new ClueId(p, n, abs));
    }

    class Scope
    {
        private SymbolGenerator _generator;
        private Dictionary<atom, int> _alreadyAssignedCount;
        private Dictionary<atom, int> _alreadyAssignedAbs;

        internal Scope(SymbolGenerator generator)
        {
            _generator = generator;
            _alreadyAssignedCount = new Dictionary<atom, int>();
            _alreadyAssignedAbs = new Dictionary<atom, int>();
        }

        public A Generate<A>(atom name, Func<atom, int, int, A> build)
        {
            if (_alreadyAssignedCount.ContainsKey(name))
            {
                return build(name, _alreadyAssignedCount[name], _alreadyAssignedAbs[name]);
            }
            return _generator.Generate(name, (p, n, abs) =>
            {
                _alreadyAssignedCount[p] = n;
                _alreadyAssignedAbs[p] = abs;
                return build(p, n, abs);
            });
        }

        public ClueId GenerateClueId(atom name) =>
            Generate(name, (p, n, abs) => new ClueId(p, n, abs));

        public type Unfree(free<type> ftype)
        {
            var val = ftype.UnsafeValue;

            var newConstraints = from c in val.Constraints select Unfree(c);
            var newTerm = Unfree(val.Term);

            return new type(newConstraints, newTerm);
        }

        public term Unfree(free<term> fterm)
        {
            var val = fterm.UnsafeValue;

            return Unfree(val);
        }

        private term Unfree(term term)
        {
            switch (term.Kind)
            {
                case TermKind.Atom: return term;
                case TermKind.Apply: return term.Apply(Unfree(term.ApplyF), Unfree(term.ApplyX));
                case TermKind.Var: return Generate(term.VarName, (a, x, y) => term.Var(new atom(a.Value + "_" + x))); // TODO: This should be a utility op in atom
            }
            throw new Exception("unreachable case");
        }
    }
}
