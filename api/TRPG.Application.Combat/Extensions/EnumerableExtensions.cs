namespace TRPG.Application.Combat.Extensions;

internal static class EnumerableExtensions
{
    public static T[] Shuffled<T>(this IEnumerable<T> source)
    {
        var array = source.ToArray();
        Random.Shared.Shuffle(array);
        return array;
    }
}
