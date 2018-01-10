using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeSystem
{
    class Program
    {
        static void Main(string[] args)
        {
            // TODO: Handle replacing a var with a type that doesn't satisfy its constraints
            var inf = new Inferrer<object, object>(new FunctionalDependencies());

            StandardRules.AddStandardRules(inf);

            var int1 = inf.CreateClue(new atom("ConstantValue"), null, 0);
            var str1 = inf.CreateClue(new atom("ConstantValue"), null, "zero");

            inf.Process();
            Debug.WriteLine(inf.Type(int1));
            Debug.WriteLine(string.Join(", ", inf.Errors(int1)));
            Debug.WriteLine(inf.Type(str1));
            Debug.WriteLine(string.Join(", ", inf.Errors(str1)));

            var str2 = inf.CreateClue(new atom("AssertType"), null, free.Of(type.Atom(new atom("String"))), str1);

            inf.Process();
            Debug.WriteLine(inf.Type(int1));
            Debug.WriteLine(string.Join(", ", inf.Errors(int1)));
            Debug.WriteLine(inf.Type(str2));
            Debug.WriteLine(string.Join(", ", inf.Errors(str2)));

            var bogus = inf.CreateClue(new atom("AssertType"), null, free.Of(type.Atom(new atom("NotInt"))), int1);
            inf.Process();
            Debug.WriteLine(inf.Type(int1));
            Debug.WriteLine(string.Join(", ", inf.Errors(int1)));
            Debug.WriteLine(inf.Type(bogus));
            Debug.WriteLine(string.Join(", ", inf.Errors(bogus)));
        }
    }
}
