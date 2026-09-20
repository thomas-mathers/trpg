namespace TRPG.Tests.Helpers;

// Deterministic stand-in for System.Random. QuestChainBlockGraphComposer routes every random
// decision through NextDouble(), so overriding just this one virtual member fully controls its
// output — useful for forcing a specific composer decision (e.g. picking a particular block type)
// in a test.
public sealed class FixedRandom(double value) : Random
{
    public override double NextDouble() => value;
}
