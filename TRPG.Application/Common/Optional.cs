namespace TRPG.Application.Common;

public readonly record struct Optional<T>
{
    public bool IsSet { get; init; }
    public T Value { get; init; }

    public static Optional<T> Unset => default;

    public static Optional<T> Of(T value) => new() { IsSet = true, Value = value };
}
