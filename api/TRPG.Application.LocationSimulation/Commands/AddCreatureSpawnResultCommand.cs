using TRPG.Application.Common.Commands;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Factions.Commands;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

// One or more freshly-generated creatures plus everything that makes them real in the world —
// their gear, skills, schedule, and (if hostile) the faction-aligned group that fights as one.
// Spans five modules' own persistence, so this fans out to each one's Add*Command rather than
// reaching into their tables directly.
public class AddCreatureSpawnResultCommand
{
    public required IReadOnlyList<CreatureGeneratorResult> Monsters { get; init; }
    public required IReadOnlyList<CreatureJob> Jobs { get; init; }
    public required IReadOnlyList<EncounterGroup> EncounterGroups { get; init; }
    public required IReadOnlyList<EncounterGroupMember> EncounterGroupMembers { get; init; }
    public required IReadOnlyList<FactionMember> FactionMembers { get; init; }
    public IReadOnlyList<Item> ExtraItems { get; init; } = [];
}

internal class AddCreatureSpawnResultCommandHandler(
    ICommandHandler<AddCreaturesCommand> addCreatures,
    ICommandHandler<AddItemsCommand> addItems,
    ICommandHandler<AddCreatureSkillsCommand> addCreatureSkills,
    ICommandHandler<AddCreatureJobsCommand> addCreatureJobs,
    ICommandHandler<CreateEncounterGroupsCommand> createEncounterGroups,
    ICommandHandler<AddFactionMembersCommand> addFactionMembers
) : ICommandHandler<AddCreatureSpawnResultCommand>
{
    public async Task Handle(
        AddCreatureSpawnResultCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await addCreatures.Handle(
            new AddCreaturesCommand
            {
                Creatures = command.Monsters.Select(monster => monster.Creature).ToArray(),
            },
            cancellationToken
        );
        await addItems.Handle(
            new AddItemsCommand
            {
                Items =
                [
                    .. command.Monsters.SelectMany(monster => monster.Items),
                    .. command.ExtraItems,
                ],
            },
            cancellationToken
        );
        await addCreatureSkills.Handle(
            new AddCreatureSkillsCommand
            {
                Skills = command.Monsters.SelectMany(monster => monster.Skills).ToArray(),
            },
            cancellationToken
        );
        await addCreatureJobs.Handle(
            new AddCreatureJobsCommand { Jobs = command.Jobs },
            cancellationToken
        );
        await createEncounterGroups.Handle(
            new CreateEncounterGroupsCommand
            {
                Groups = command.EncounterGroups,
                Members = command.EncounterGroupMembers,
            },
            cancellationToken
        );
        await addFactionMembers.Handle(
            new AddFactionMembersCommand { Members = command.FactionMembers },
            cancellationToken
        );
    }
}
