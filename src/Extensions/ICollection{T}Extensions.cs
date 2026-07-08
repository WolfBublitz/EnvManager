using System.Collections.Generic;

internal static class ICollectionExtensions
{
    internal static ICollection<T> AddIfTrue<T>(this ICollection<T> @this, bool condition, T item)
    {
        if (condition)
        {
            @this.Add(item);
        }

        return @this;
    }
}
