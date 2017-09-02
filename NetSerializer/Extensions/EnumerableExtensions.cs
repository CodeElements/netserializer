using System.Collections.Generic;

namespace NetSerializer.Extensions
{
	internal static class EnumerableExtensions
	{
		public static IEnumerable<T> ToEnumerable<T>(this T item)
		{
			yield return item;
		}
	}
}
