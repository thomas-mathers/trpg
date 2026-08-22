namespace TRPG.Application.Reputations;

public enum ReputationAttitude
{
    Hostile,
    Wary,
    Neutral,
    Warm,
    Trusting,
}

public static class ReputationAttitudeCalculator
{
    public static ReputationAttitude FromScore(int score) =>
        score switch
        {
            <= -50 => ReputationAttitude.Hostile,
            <= -15 => ReputationAttitude.Wary,
            < 15 => ReputationAttitude.Neutral,
            < 50 => ReputationAttitude.Warm,
            _ => ReputationAttitude.Trusting,
        };
}
