namespace TRPG.Application.WorldSimulation.Commands;

public sealed record TouchedCreatures(Guid LocationId, IReadOnlyCollection<Guid> CreatureIds);
