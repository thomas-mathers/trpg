namespace TRPG.Application.Algorithms;

internal static class WeightedSampler
{
    public static int SampleIndex(IReadOnlyList<int> weights)
    {
        var totalWeight = weights.Sum();
        if (totalWeight <= 0)
        {
            return Random.Shared.Next(weights.Count);
        }

        var roll = Random.Shared.Next(totalWeight);
        var cumulative = 0;
        for (var i = 0; i < weights.Count; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
            {
                return i;
            }
        }

        return weights.Count - 1;
    }
}
