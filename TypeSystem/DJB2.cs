using System.Collections.Generic;

namespace TypeSystem
{
    static class DJB2
    {
        public static int Hash(params object[] os) {
            int hash = 5381;

            foreach (var o in os) {
                hash = ((hash << 5) + hash) + (o?.GetHashCode() ?? 0); 
			}

            return hash;
        }
		public static int HashIEnumerable<T>(IEnumerable<T> os)
		{
			int hash = 5381;

			foreach (var o in os)
			{
				hash = ((hash << 5) + hash) + (o?.GetHashCode() ?? 0);
			}

			return hash;
		}
	}
}
