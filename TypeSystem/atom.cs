using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeSystem
{
    struct atom
    {
        public readonly string Value; // we define Value=null to be a standard-issue empty atom

        public atom(string s)
        {
            Value = s;
        }

        public bool Equals(atom other)
        {
            return string.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is atom && Equals((atom) obj);
        }

        public override int GetHashCode()
        {
            return (Value != null ? Value.GetHashCode() : 0);
        }

        public static bool operator ==(atom left, atom right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(atom left, atom right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{Value}";
        }
    }
}
