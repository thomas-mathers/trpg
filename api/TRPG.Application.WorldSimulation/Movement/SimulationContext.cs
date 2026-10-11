using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Movement;

public sealed record SimulationContext(
    GameInstant Now,
    IReadOnlySet<Guid> SevereWeatherLocationIds
);

internal sealed record CreatureTickInput(SimulatedCreature Creature, SimulationContext Context);
