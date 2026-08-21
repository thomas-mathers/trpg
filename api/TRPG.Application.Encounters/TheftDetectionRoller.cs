namespace TRPG.Application.Encounters;

internal interface ITheftDetectionRoller
{
    bool IsDetected(float chance);
}

internal class TheftDetectionRoller : ITheftDetectionRoller
{
    public bool IsDetected(float chance) => Random.Shared.NextDouble() < chance;
}
