namespace TypeSystem
{
    class ClueId
    {
        public readonly atom Word;
        public readonly int Number;
        public readonly int AbsoluteNumber; // for ordering

        public ClueId(atom word, int number, int absolute)
        {
            Word = word;
            Number = number;
            AbsoluteNumber = absolute;
        }

        protected bool Equals(ClueId other)
        {
            return Word.Equals(other.Word) && Number == other.Number;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ClueId) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Word.GetHashCode() * 397) ^ Number;
            }
        }

        public static bool operator ==(ClueId left, ClueId right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ClueId left, ClueId right)
        {
            return !Equals(left, right);
        }
    }
}