namespace TRPG.Application.Common.Extensions;

public static class EnumerableExtensions
{
    public static T[] Shuffled<T>(this IEnumerable<T> source)
    {
        var array = source.ToArray();
        Random.Shared.Shuffle(array);
        return array;
    }
}
