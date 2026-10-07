namespace TRPG.Application.Common.Navigation;

public static class InLocationPace
{
    private const double MetersPerSecond = 3;
    private const double BaseMovementSpeed = 50;
    private const double SecondsPerHour = 3600;

    public static double MetersPerGameSecond(float movementSpeed, double timeScale) =>
        MetersPerSecond * movementSpeed / BaseMovementSpeed / timeScale;

    public static double MetersPerGameHour(float movementSpeed, double timeScale) =>
        MetersPerGameSecond(movementSpeed, timeScale) * SecondsPerHour;
}
