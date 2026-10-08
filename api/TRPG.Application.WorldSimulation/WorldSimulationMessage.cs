using TRPG.Application.WorldSimulation.Movement;

namespace TRPG.Application.WorldSimulation;

public abstract record WorldSimulationMessage;

public sealed record EngageCreature(Guid CreatureId) : WorldSimulationMessage;

public sealed record ReleaseCreature(Guid CreatureId) : WorldSimulationMessage;

public sealed record SpawnCreature(SimCreatureSeed Seed) : WorldSimulationMessage;

public sealed record RemoveCreature(Guid CreatureId) : WorldSimulationMessage;

public sealed record TrackCreature(Guid CreatureId) : WorldSimulationMessage;
