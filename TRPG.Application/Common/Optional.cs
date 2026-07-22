namespace TRPG.Application.Common;

internal readonly record struct Optional<T>
{
    public bool IsSet { get; init; }
    public T Value { get; init; }

    public static Optional<T> Unset => default;

    public static Optional<T> Of(T value) => new() { IsSet = true, Value = value };
}
