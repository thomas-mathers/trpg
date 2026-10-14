using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Fights;
using TRPG.Application.Encounters.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class CastPlayerAbilityCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid TargetId { get; init; }
    public required string AbilityName { get; init; }
    public required GameInstant GameTime { get; init; }
    public required ActiveFight? Fight { get; init; }
    public required PlayerPlacement? PlayerPlacement { get; init; }
    public required Func<Guid, Point?> FindLiveCreaturePosition { get; init; }
}

public record PlayerPlacement(Point Position, double Heading);

public record AbilityCastResult(
    bool Succeeded,
    bool AbilityNotFound,
    string? Message,
    ActiveFight? Fight
);

internal class CastPlayerAbilityCommandHandler(
    IQueryHandler<GetFightDraftQuery, FightDraft?> getFightDraft,
    ICommandHandler<OpenFightCommand, OpenFightResult> openFight,
    ICommandHandler<CastFightAbilityCommand, AbilityUseResult> castFightAbility,
    ICommandHandler<UseSupportAbilityCommand, AbilityUseResult> useSupportAbility
) : ICommandHandler<CastPlayerAbilityCommand, AbilityCastResult>
{
    public async Task<AbilityCastResult> Handle(
        CastPlayerAbilityCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Fight is { } fight)
        {
            return await CastInFight(command, fight, cancellationToken);
        }

        var draft = await getFightDraft.Handle(
            new GetFightDraftQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                TargetId = command.TargetId,
                AbilityName = command.AbilityName,
            },
            cancellationToken
        );
        if (draft is null)
        {
            return Refused("You can't do that right now.");
        }

        Place(draft.Combatants, command);

        return draft.OpensFight
            ? await OpenFight(command, draft, cancellationToken)
            : await CastSupport(command, draft, cancellationToken);
    }

    private async Task<AbilityCastResult> CastInFight(
        CastPlayerAbilityCommand command,
        ActiveFight fight,
        CancellationToken cancellationToken
    )
    {
        Place(fight.Combatants, command);
        var use = await castFightAbility.Handle(
            new CastFightAbilityCommand
            {
                Fight = fight,
                TargetId = command.TargetId,
                AbilityName = command.AbilityName,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

        return Map(use, IsFightOver(use) ? null : fight);
    }

    private async Task<AbilityCastResult> OpenFight(
        CastPlayerAbilityCommand command,
        FightDraft draft,
        CancellationToken cancellationToken
    )
    {
        var opened = await openFight.Handle(
            new OpenFightCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                TargetId = command.TargetId,
                AbilityName = command.AbilityName,
                Draft = draft,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

        return Map(opened.Use, IsFightOver(opened.Use) ? null : opened.Fight);
    }

    private async Task<AbilityCastResult> CastSupport(
        CastPlayerAbilityCommand command,
        FightDraft draft,
        CancellationToken cancellationToken
    )
    {
        var use = await useSupportAbility.Handle(
            new UseSupportAbilityCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                TargetId = command.TargetId,
                AbilityName = command.AbilityName,
                Combatants = draft.Combatants,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

        return Map(use, null);
    }

    private static void Place(IReadOnlyList<Combatant> combatants, CastPlayerAbilityCommand command)
    {
        foreach (var combatant in combatants)
        {
            if (combatant.IsPlayer)
            {
                if (command.PlayerPlacement is { } placement)
                {
                    combatant.Position = placement.Position;
                    combatant.Heading = placement.Heading;
                }
            }
            else if (command.FindLiveCreaturePosition(combatant.CreatureId) is { } position)
            {
                combatant.Position = position;
            }
        }
    }

    private static bool IsFightOver(AbilityUseResult use) =>
        use.State?.Outcome is CombatOutcome.Victory or CombatOutcome.Defeat or CombatOutcome.Fled;

    private static AbilityCastResult Map(AbilityUseResult use, ActiveFight? fight) =>
        new(use.Succeeded, use.Rejection is AbilityUseRejection.NotFound, use.Message, fight);

    private static AbilityCastResult Refused(string message) => new(false, false, message, null);
}
