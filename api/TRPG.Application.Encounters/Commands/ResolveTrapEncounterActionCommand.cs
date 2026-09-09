using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ResolveTrapEncounterActionCommand : IEncounterResolutionCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid SessionId { get; init; }
    public required TrapEncounterAction Action { get; init; }
    public required Guid EncounterId { get; init; }
}

internal class ResolveTrapEncounterActionCommandHandler(
    IEncountersDbContext context,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    ICommandHandler<MarkTrapResolvedCommand> markTrapResolved,
    ICommandHandler<MovePlayerCommand> movePlayer,
    SkillCheckService skillCheckService,
    IChanceRoller chanceRoller,
    IOptionsSnapshot<TrapOptions> trapOptions,
    IOptionsSnapshot<LockpickingOptions> lockpickingOptions
)
    : EncounterResolutionCommandHandlerBase<
        TrapEncounter,
        ResolveTrapEncounterActionCommand,
        TrapEncounterResolutionFact
    >(context)
{
    protected override async Task<TrapEncounterResolutionFact> Resolve(
        ResolveTrapEncounterActionCommand command,
        TrapEncounter encounter,
        CancellationToken cancellationToken
    ) =>
        command.Action switch
        {
            WithdrawTrapAction => new TrapEncounterResolutionFact(
                encounter.Id,
                TrapEncounterResolutionOutcome.Withdrew,
                encounter.TrapKind,
                null
            ),
            DisarmTrapAction => await Disarm(command, encounter, cancellationToken),
            AttemptTrapAction => await Attempt(command, encounter, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

    private async Task<TrapEncounterResolutionFact> Disarm(
        ResolveTrapEncounterActionCommand command,
        TrapEncounter encounter,
        CancellationToken cancellationToken
    )
    {
        if (encounter.TrapKind != TrapKind.Mechanical)
        {
            throw new InvalidOperationException(
                $"Only a mechanical trap can be disarmed; this one is {encounter.TrapKind}."
            );
        }

        var options = lockpickingOptions.Value;
        var curve = new SkillCheckCurve(
            BaseChance: options.BaseChance,
            ChanceChangePerSkillLevel: options.ChancePerSkillLevel,
            MinimumChance: options.MinimumChance,
            MaximumChance: options.MaximumChance
        );
        var succeeded = await skillCheckService.Roll(
            command.PlayerId,
            Skill.Lockpicking,
            curve,
            cancellationToken
        );

        if (!succeeded)
        {
            return await Fall(command, encounter, cancellationToken);
        }

        await markTrapResolved.Handle(
            new MarkTrapResolvedCommand { TriggerId = encounter.TriggerId },
            cancellationToken
        );
        return new TrapEncounterResolutionFact(
            encounter.Id,
            TrapEncounterResolutionOutcome.Disarmed,
            encounter.TrapKind,
            null
        );
    }

    private async Task<TrapEncounterResolutionFact> Attempt(
        ResolveTrapEncounterActionCommand command,
        TrapEncounter encounter,
        CancellationToken cancellationToken
    )
    {
        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = command.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), command.PlayerId);

        var options = trapOptions.Value;
        var chance = Math.Clamp(
            options.BaseAttemptChance + player.Dexterity * options.AttemptChancePerDexterityPoint,
            options.MinimumAttemptChance,
            options.MaximumAttemptChance
        );
        if (!chanceRoller.Roll(chance))
        {
            return await Fall(command, encounter, cancellationToken);
        }

        await markTrapResolved.Handle(
            new MarkTrapResolvedCommand { TriggerId = encounter.TriggerId },
            cancellationToken
        );
        return new TrapEncounterResolutionFact(
            encounter.Id,
            TrapEncounterResolutionOutcome.Survived,
            encounter.TrapKind,
            null
        );
    }

    private async Task<TrapEncounterResolutionFact> Fall(
        ResolveTrapEncounterActionCommand command,
        TrapEncounter encounter,
        CancellationToken cancellationToken
    )
    {
        await markTrapResolved.Handle(
            new MarkTrapResolvedCommand { TriggerId = encounter.TriggerId },
            cancellationToken
        );

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );
        await movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = command.PlayerId,
                DestinationLocationId = encounter.TargetLocationId,
                Playtime = playtime,
            },
            cancellationToken
        );

        return new TrapEncounterResolutionFact(
            encounter.Id,
            TrapEncounterResolutionOutcome.Fell,
            encounter.TrapKind,
            encounter.TargetLocationName
        );
    }
}
