namespace TRPG.Domain;

public readonly record struct GameInstant : IComparable<GameInstant>
{
    public GameInstant(DateTime value)
    {
        if (value.Kind != DateTimeKind.Unspecified)
        {
            throw new ArgumentException(
                "A game instant must have an unspecified DateTime kind.",
                nameof(value)
            );
        }

        Value = value;
    }

    public DateTime Value { get; }

    public int CompareTo(GameInstant other) => Value.CompareTo(other.Value);

    public GameInstant Add(TimeSpan duration) => new(Value + duration);

    public GameInstant Subtract(TimeSpan duration) => new(Value - duration);

    public TimeSpan Subtract(GameInstant instant) => Value - instant.Value;

    public static GameInstant operator +(GameInstant instant, TimeSpan duration) =>
        instant.Add(duration);

    public static GameInstant operator +(TimeSpan duration, GameInstant instant) =>
        instant + duration;

    public static GameInstant operator -(GameInstant instant, TimeSpan duration) =>
        instant.Subtract(duration);

    public static TimeSpan operator -(GameInstant left, GameInstant right) => left.Subtract(right);

    public static bool operator <(GameInstant left, GameInstant right) => left.Value < right.Value;

    public static bool operator <=(GameInstant left, GameInstant right) =>
        left.Value <= right.Value;

    public static bool operator >(GameInstant left, GameInstant right) => left.Value > right.Value;

    public static bool operator >=(GameInstant left, GameInstant right) =>
        left.Value >= right.Value;
}
