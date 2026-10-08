namespace TRPG.Application.Common.Navigation;

public static class InLocationPace
{
    public const double WalkMetersPerSecond = 1.4;
    public const double BaseMovementSpeed = 50;
    private const double SecondsPerHour = 3600;

    public static double MetersPerRealSecond(float movementSpeed) =>
        WalkMetersPerSecond * movementSpeed / BaseMovementSpeed;

    public static double MetersPerGameSecond(float movementSpeed, double timeScale) =>
        MetersPerRealSecond(movementSpeed) / timeScale;

    public static double MetersPerGameHour(float movementSpeed, double timeScale) =>
        MetersPerGameSecond(movementSpeed, timeScale) * SecondsPerHour;
}
