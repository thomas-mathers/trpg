using TRPG.Domain;

namespace TRPG.Combat.Mappers;

internal static class GameInstantMapper
{
    public static long ToGameTimeMilliseconds(this GameInstant instant) =>
        (long)(instant - GameClock.Epoch).TotalMilliseconds;
}
