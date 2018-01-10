using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeSystem
{
    class StandardRules
    {
        public static void AddStandardRules<ClueSource>(Inferrer<ClueSource, object> inferrer)
        {
            inferrer.CreateRule(RuleConstant);
            inferrer.CreateRule(RuleAssert);
        }

        private static void RuleConstant(IInferenceTools<object> tools)
        {
            if (tools.Kind(0) != new atom("ConstantValue")) { return; }

            tools.PreconditionsAreSatisfied();

            var present = tools.Constant(0);
            if (present is int) {
                tools.Unify(free.Of(type.Atom(new atom("Int"))), 0);
                return;
            }
            if (present is string) {
                tools.Unify(free.Of(type.Atom(new atom("String"))), 0);
                return;
            }
            tools.Fail($"unrecognized constant type for {present}", 0);
        }

        private static void RuleAssert(IInferenceTools<object> tools)
        {
            if (tools.Kind(0) != new atom("AssertType")) { return; }

            tools.PreconditionsAreSatisfied();
            var present = tools.Constant(0);
            if (!(present is free<type>)) { tools.Fail($"for AssertType, constant must be a free<type>"); return; }

            tools.Unify(0, 1);
            tools.Unify((free<type>) present, 0);
        }
    }
}
