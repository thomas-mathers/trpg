using TRPG.Application.Common.Navigation;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation;

public abstract record WorldSimulationMessage;

public sealed record EngageCreature(Guid CreatureId) : WorldSimulationMessage;

public sealed record ReleaseCreature(Guid CreatureId) : WorldSimulationMessage;

public sealed record SpawnCreature(SimCreatureSeed Seed) : WorldSimulationMessage;

public sealed record RemoveCreature(Guid CreatureId) : WorldSimulationMessage;

public sealed record TrackCreature(Guid CreatureId) : WorldSimulationMessage;

public sealed record PlayerInput(
    Guid PlayerId,
    Guid LocationId,
    MovementInput Input,
    Point ClientPosition,
    DateTimeOffset ReceivedAt
) : WorldSimulationMessage;
