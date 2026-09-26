using TRPG.Application.Combat;
using TRPG.Application.Combat.Extensions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Events;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class StartFightCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required IReadOnlyCollection<Guid> EnemyCreatureIds { get; init; }
    public required bool HasSurpriseRound { get; init; }
}

internal class StartFightCommandHandler(
    IEncountersDbContext context,
    CombatantFactory combatantFactory,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    IGameClientEventSink gameEvents
) : ICommandHandler<StartFightCommand>
{
    public async Task Handle(
        StartFightCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

        var regeneratedCreatures = await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand
            {
                GameTime = gameTime,
                CreatureIds = [command.PlayerId, .. command.EnemyCreatureIds],
            },
            cancellationToken
        );

        var combatants = await combatantFactory.CreateMany(
            command.WorldId,
            regeneratedCreatures.Values.ToArray(),
            command.PlayerId,
            cancellationToken
        );

        var fight = new FightEncounter
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = player!.LocationId,
            CombatantIds = combatants.Select(c => c.CreatureId).ToList(),
            HasSurpriseRound = command.HasSurpriseRound,
        };

        context.Encounters.Add(fight);

        await context.SaveChangesAsync(cancellationToken);

        gameEvents.Enqueue(
            new CombatStartedEvent(
                fight.Id,
                combatants
                    .OrderByDescending(combatant => combatant.TurnOrder)
                    .Select(combatant => combatant.ToCombatantResult())
                    .ToArray()
            )
        );
    }
}
