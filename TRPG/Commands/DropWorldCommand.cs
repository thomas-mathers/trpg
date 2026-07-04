using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Commands;

internal class DropWorldCommand {
    public required Guid WorldId { get; init; }
}

internal class DropWorldCommandHandler(TrpgDbContext context) {
    public async Task Handle(DropWorldCommand command, CancellationToken cancellationToken = default) {
        var worldId = command.WorldId;

        await context.ContainerItems.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.RoomConnectorKeys.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Props.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Rooms.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.InventoryItems.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.BuildingOwners.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.FactionMembers.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Reputations.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.PersonQuestObjectives.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.PersonQuests.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.QuestObjectives.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Quests.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.PersonAbilities.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.PersonSkills.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Jobs.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.NpcConversations.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.WorldEvents.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Items.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Buildings.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Districts.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Cities.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Roads.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.States.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Countries.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Races.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Factions.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Persons.Where(x => x.WorldId == worldId).ExecuteDeleteAsync(cancellationToken);
        await context.Worlds.Where(x => x.Id == worldId).ExecuteDeleteAsync(cancellationToken);
    }
}