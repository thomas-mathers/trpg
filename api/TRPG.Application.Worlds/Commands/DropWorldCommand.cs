using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data;

namespace TRPG.Application.Worlds.Commands;

public class DropWorldCommand
{
    public required Guid WorldId { get; init; }
}

internal class DropWorldCommandHandler(TrpgDbContext context) : ICommandHandler<DropWorldCommand>
{
    public async Task Handle(
        DropWorldCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var worldId = command.WorldId;

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );

        await context
            .EncounterGroupMembers.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .EncounterGroups.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .Encounters.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .CrimeWitnesses.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.Crimes.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);

        await context
            .DoorConnectorKeys.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .DoorConnectors.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .TravelConnectors.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .LocationConnectors.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.Props.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);

        await context
            .RoomBookings.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.Rooms.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);

        await context
            .Locations.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .BuildingOwners.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .FactionMembers.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .Relationships.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .Reputations.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .ReputationLogEntries.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .CreatureQuestObjectives.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .CreatureQuests.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .QuestObjectives.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.Quests.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);

        await context
            .CreatureSkills.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .CreatureWeaponProficiencies.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .CreatureJobs.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .NpcConversations.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .NpcConversationHistories.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .CreatureProfiles.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .CreatureKnowledge.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.Items.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);

        await context
            .Buildings.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .Districts.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.Cities.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);

        await context.States.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);

        await context
            .Countries.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .Factions.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .Creatures.Where(x => x.WorldId == worldId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.Worlds.Where(x => x.Id == worldId).ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
