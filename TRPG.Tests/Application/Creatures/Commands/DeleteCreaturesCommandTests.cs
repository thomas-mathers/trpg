using Microsoft.EntityFrameworkCore;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class DeleteCreaturesCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private DeleteCreaturesCommandHandler _handler = null!;
    private Guid _worldId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new DeleteCreaturesCommandHandler(_context);
        _worldId = Guid.NewGuid();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DeletesEveryReferencingRow_AndLeavesOtherCreaturesUntouched()
    {
        // Arrange
        var target = Builders.MakeCreature(_worldId);
        var other = Builders.MakeCreature(_worldId);
        _context.Creatures.AddRange(target, other);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var quest = Builders.MakeQuest(other.Id, _worldId);
        _context.Quests.Add(quest);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var questObjective = new QuestObjective
        {
            QuestId = quest.Id,
            Name = "Slay",
            Description = "A test objective",
            Target = target.Id,
            TargetType = QuestTargetType.Creature,
            Type = QuestObjectiveType.Kill,
            WorldId = _worldId,
        };
        _context.QuestObjectives.Add(questObjective);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var building = Builders.MakeBuilding(Guid.NewGuid(), worldId: _worldId);
        var room = Builders.MakeRoom(building.Id, worldId: _worldId);
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bed = new Bed
        {
            RoomId = room.Id,
            Name = "Bed",
            Description = "A test bed",
            WorldId = _worldId,
            AssignedCreatureId = target.Id,
            OccupantId = target.Id,
        };
        var workstation = new Workstation
        {
            RoomId = room.Id,
            Name = "Workstation",
            Description = "A test workstation",
            WorldId = _worldId,
            WorkstationType = WorkstationType.Cooking,
            AssignedCreatureId = target.Id,
            OccupantId = target.Id,
        };
        _context.Props.AddRange(bed, workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await SeedCreatureData(target.Id, quest.Id, questObjective.Id, building.Id, room.Id);
        await SeedCreatureData(other.Id, quest.Id, questObjective.Id, building.Id, room.Id);

        // Act
        await _handler.Handle(
            new DeleteCreaturesCommand { CreatureIds = [target.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.False(
            await verifyContext.Creatures.AnyAsync(x => x.Id == target.Id, cancellationToken)
        );
        Assert.False(
            await verifyContext.CreatureAbilities.AnyAsync(
                x => x.CreatureId == target.Id,
                cancellationToken
            )
        );
        Assert.False(
            await verifyContext.CreatureSkills.AnyAsync(
                x => x.CreatureId == target.Id,
                cancellationToken
            )
        );
        Assert.False(
            await verifyContext.CreatureWeaponProficiencies.AnyAsync(
                x => x.CreatureId == target.Id,
                cancellationToken
            )
        );
        Assert.False(
            await verifyContext.CreatureQuestObjectives.AnyAsync(
                x => x.CreatureId == target.Id,
                cancellationToken
            )
        );
        Assert.False(
            await verifyContext.CreatureQuests.AnyAsync(
                x => x.CreatureId == target.Id,
                cancellationToken
            )
        );
        Assert.False(
            await verifyContext.InventoryItems.AnyAsync(
                x => x.CreatureId == target.Id,
                cancellationToken
            )
        );
        Assert.False(
            await verifyContext.CreatureJobs.AnyAsync(
                x => x.CreatureId == target.Id,
                cancellationToken
            )
        );
        Assert.False(
            await verifyContext.FactionMembers.AnyAsync(
                x => x.CreatureId == target.Id,
                cancellationToken
            )
        );
        Assert.False(
            await verifyContext.NpcConversations.AnyAsync(
                x => x.CreatureId == target.Id,
                cancellationToken
            )
        );
        Assert.False(
            await verifyContext.NpcConversations.AnyAsync(
                x => x.NpcId == target.Id,
                cancellationToken
            )
        );
        Assert.False(
            await verifyContext.BuildingOwners.AnyAsync(
                x => x.OwnerId == target.Id,
                cancellationToken
            )
        );
        Assert.False(
            await verifyContext.Reputations.AnyAsync(
                x => x.CreatureId == target.Id,
                cancellationToken
            )
        );
        Assert.False(
            await verifyContext.Relationships.AnyAsync(
                x => x.SubjectId == target.Id || x.RelativeId == target.Id,
                cancellationToken
            )
        );
        Assert.False(
            await verifyContext.CreatureKnowledge.AnyAsync(
                x => x.KnowerId == target.Id || x.SubjectId == target.Id,
                cancellationToken
            )
        );

        var updatedBed = await verifyContext
            .Set<Bed>()
            .SingleAsync(x => x.Id == bed.Id, cancellationToken);
        Assert.Null(updatedBed.AssignedCreatureId);
        Assert.Null(updatedBed.OccupantId);

        var updatedWorkstation = await verifyContext
            .Set<Workstation>()
            .SingleAsync(x => x.Id == workstation.Id, cancellationToken);
        Assert.Null(updatedWorkstation.AssignedCreatureId);
        Assert.Null(updatedWorkstation.OccupantId);

        Assert.False(
            await verifyContext
                .Set<Seat>()
                .AnyAsync(seat => seat.OccupantId == target.Id, cancellationToken)
        );

        Assert.True(
            await verifyContext.Creatures.AnyAsync(x => x.Id == other.Id, cancellationToken)
        );
        Assert.True(
            await verifyContext.CreatureAbilities.AnyAsync(
                x => x.CreatureId == other.Id,
                cancellationToken
            )
        );
        Assert.True(
            await verifyContext.CreatureSkills.AnyAsync(
                x => x.CreatureId == other.Id,
                cancellationToken
            )
        );
        Assert.True(
            await verifyContext.CreatureWeaponProficiencies.AnyAsync(
                x => x.CreatureId == other.Id,
                cancellationToken
            )
        );
        Assert.True(
            await verifyContext.CreatureQuestObjectives.AnyAsync(
                x => x.CreatureId == other.Id,
                cancellationToken
            )
        );
        Assert.True(
            await verifyContext.CreatureQuests.AnyAsync(
                x => x.CreatureId == other.Id,
                cancellationToken
            )
        );
        Assert.True(
            await verifyContext.InventoryItems.AnyAsync(
                x => x.CreatureId == other.Id,
                cancellationToken
            )
        );
        Assert.True(
            await verifyContext.CreatureJobs.AnyAsync(
                x => x.CreatureId == other.Id,
                cancellationToken
            )
        );
        Assert.True(
            await verifyContext.FactionMembers.AnyAsync(
                x => x.CreatureId == other.Id,
                cancellationToken
            )
        );
        Assert.True(
            await verifyContext.NpcConversations.AnyAsync(
                x => x.CreatureId == other.Id,
                cancellationToken
            )
        );
        Assert.True(
            await verifyContext.NpcConversations.AnyAsync(
                x => x.NpcId == other.Id,
                cancellationToken
            )
        );
        Assert.True(
            await verifyContext.BuildingOwners.AnyAsync(
                x => x.OwnerId == other.Id,
                cancellationToken
            )
        );
        Assert.True(
            await verifyContext
                .Set<Seat>()
                .AnyAsync(seat => seat.OccupantId == other.Id, cancellationToken)
        );
        Assert.True(
            await verifyContext.Reputations.AnyAsync(
                x => x.CreatureId == other.Id,
                cancellationToken
            )
        );
        Assert.True(
            await verifyContext.Relationships.AnyAsync(
                x => x.SubjectId == other.Id || x.RelativeId == other.Id,
                cancellationToken
            )
        );
        Assert.True(
            await verifyContext.CreatureKnowledge.AnyAsync(
                x => x.KnowerId == other.Id || x.SubjectId == other.Id,
                cancellationToken
            )
        );
    }

    private async Task SeedCreatureData(
        Guid creatureId,
        Guid questId,
        Guid questObjectiveId,
        Guid buildingId,
        Guid roomId
    )
    {
        var faction = Builders.MakeFaction(_worldId);
        var item = Builders.MakeItem(_worldId);
        _context.Factions.Add(faction);
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _context.CreatureAbilities.Add(
            new CreatureAbility
            {
                CreatureId = creatureId,
                AbilityName = "Strike",
                WorldId = _worldId,
            }
        );
        _context.CreatureSkills.Add(
            new CreatureSkill
            {
                CreatureId = creatureId,
                Skill = Skill.Swordsmanship,
                Level = 1,
                WorldId = _worldId,
            }
        );
        _context.CreatureWeaponProficiencies.Add(
            new CreatureWeaponProficiency
            {
                CreatureId = creatureId,
                WeaponType = WeaponType.Sword,
                Proficiency = 10,
                WorldId = _worldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            new CreatureQuestObjective
            {
                CreatureId = creatureId,
                ObjectiveId = questObjectiveId,
                Amount = 1,
                WorldId = _worldId,
            }
        );
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = creatureId,
                QuestId = questId,
                Status = QuestStatus.Accepted,
                WorldId = _worldId,
            }
        );
        _context.InventoryItems.Add(
            new InventoryItem
            {
                CreatureId = creatureId,
                ItemId = item.Id,
                Quantity = 1,
                WorldId = _worldId,
            }
        );
        _context.CreatureJobs.Add(Builders.MakeCreatureJob(creatureId, worldId: _worldId));
        _context.FactionMembers.Add(
            new FactionMember
            {
                CreatureId = creatureId,
                FactionId = faction.Id,
                Role = FactionRole.Member,
                WorldId = _worldId,
            }
        );
        _context.NpcConversations.Add(
            new NpcConversation
            {
                CreatureId = creatureId,
                NpcId = Guid.NewGuid(),
                Summary = "A test conversation",
                WorldId = _worldId,
            }
        );
        _context.NpcConversations.Add(
            new NpcConversation
            {
                CreatureId = Guid.NewGuid(),
                NpcId = creatureId,
                Summary = "A test conversation about them",
                WorldId = _worldId,
            }
        );
        _context.BuildingOwners.Add(
            new BuildingOwner
            {
                BuildingId = buildingId,
                OwnerId = creatureId,
                WorldId = _worldId,
            }
        );
        _context.Props.Add(
            new Seat
            {
                RoomId = roomId,
                Name = "Seat",
                Description = "A test seat",
                WorldId = _worldId,
                OccupantId = creatureId,
            }
        );
        _context.Reputations.Add(
            new Reputation
            {
                CreatureId = creatureId,
                TargetId = faction.Id,
                TargetType = ReputationTargetType.Faction,
                Score = 10,
                WorldId = _worldId,
            }
        );
        _context.Relationships.Add(
            new Relationship
            {
                SubjectId = creatureId,
                RelativeId = Guid.NewGuid(),
                RelationshipType = RelationshipType.Sister,
                WorldId = _worldId,
            }
        );
        _context.CreatureKnowledge.Add(
            new CreatureKnowledge
            {
                KnowerId = creatureId,
                SubjectId = Guid.NewGuid(),
                SubjectType = KnowledgeSubjectType.Country,
                WorldId = _worldId,
            }
        );

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
