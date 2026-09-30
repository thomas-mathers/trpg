using TRPG.Application.Combat;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class OpenFightWithAbilityCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid TargetId { get; init; }
    public required string AbilityName { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class OpenFightWithAbilityCommandHandler(
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    IQueryHandler<
        GetAbilityAvailabilityQuery,
        IReadOnlyList<AbilityAvailability>
    > getAbilityAvailability,
    ICommandHandler<AttackCreatureCommand> attackCreature,
    ICommandHandler<ResolvePlayerCombatActionCommand, PlayerCombatActionResult> resolveCombatAction
) : ICommandHandler<OpenFightWithAbilityCommand, PlayerCombatActionResult>
{
    public async Task<PlayerCombatActionResult> Handle(
        OpenFightWithAbilityCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureAbilityCanOpenFight(command, cancellationToken);

        await attackCreature.Handle(
            new AttackCreatureCommand
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                TargetId = command.TargetId,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

        return await resolveCombatAction.Handle(
            new ResolvePlayerCombatActionCommand
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                Action = new UseAbilityAction(command.TargetId, command.AbilityName),
                GameTime = command.GameTime,
            },
            cancellationToken
        );
    }

    private async Task EnsureAbilityCanOpenFight(
        OpenFightWithAbilityCommand command,
        CancellationToken cancellationToken
    )
    {
        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (activeEncounter is not null)
            throw new InvalidOperationException("You can't do that right now.");

        var availability = await getAbilityAvailability.Handle(
            new GetAbilityAvailabilityQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        var ability = availability.FirstOrDefault(a => a.Name == command.AbilityName);
        if (ability is null)
            throw new InvalidOperationException($"Ability {command.AbilityName} not found");
        if (!ability.IsUsable)
            throw new InvalidOperationException(
                $"{command.AbilityName} isn't usable right now: {ability.Reason}."
            );
    }
}
