using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Commands;

public sealed class DropWorldCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid OtherWorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private DropWorldCommandHandler _handler = null!;
    private readonly Dictionary<Guid, Guid> _sessionIdByWorldId = [];

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<DropWorldCommandHandler>();

        await SeedWorldData(WorldId);
        await SeedWorldData(OtherWorldId);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    private async Task SeedWorldData(Guid worldId)
    {
        var world = Builders.MakeWorld(worldId);
        var country = Builders.MakeCountry(worldId);
        var state = Builders.MakeState(country.Id, worldId);
        var city = Builders.MakeCity(state.Id, country.Id, worldId: worldId);
        var district = Builders.MakeDistrict(city.Id, worldId: worldId);
        var creature = Builders.MakeCreature(worldId);
        var companion = Builders.MakeCreature(worldId);
        var faction = Builders.MakeFaction(worldId);
        var rivalFaction = Builders.MakeFaction(worldId);
        var building = Builders.MakeBuilding(worldId: worldId);
        var room = Builders.MakeRoom(building.Id, worldId: worldId);
        var location = Builders.MakeLocation(worldId, state.Id, city.Id, district.Id, room.Id);
        var destination = Builders.MakeLocation(worldId, state.Id, city.Id, district.Id);
        var bed = new Bed
        {
            LocationId = room.LocationId,
            Name = "Bed",
            Description = "A test bed.",
            WorldId = worldId,
        };
        var item = Builders.MakeItem(worldId);
        var locationConnector = Builders.MakeLocationConnector(
            location.Id,
            destination.Id,
            worldId
        );
        var returnLocationConnector = Builders.MakeLocationConnector(
            destination.Id,
            location.Id,
            worldId
        );
        returnLocationConnector.OriginNodeId = locationConnector.DestinationNodeId;
        returnLocationConnector.DestinationNodeId = locationConnector.OriginNodeId;
        var doorConnector = Builders.MakeDoorConnector(locationConnector.Id, worldId: worldId);
        var factionMember = new FactionMember
        {
            FactionId = faction.Id,
            CreatureId = creature.Id,
            Role = FactionRole.Member,
            WorldId = worldId,
        };
        var reputationLogEntry = new ReputationLogEntry
        {
            WorldId = worldId,
            CreatureId = creature.Id,
            TargetId = faction.Id,
            TargetType = ReputationTargetType.Faction,
            DeltaScore = -1,
            Reason = ReputationReason.QuestCompleted,
        };
        var reputation = Builders.MakeReputation(worldId, creature.Id, faction.Id);
        var weaponProficiency = new CreatureWeaponProficiency
        {
            WorldId = worldId,
            CreatureId = creature.Id,
            WeaponType = WeaponType.Sword,
            Proficiency = 3,
        };
        var encounterGroup = Builders.MakeEncounterGroup(worldId, location.Id, faction.Id);
        var encounterGroupMember = Builders.MakeEncounterGroupMember(
            worldId,
            encounterGroup.Id,
            creature.Id
        );
        var encounter = Builders.MakeHostileEncounter(worldId, creature.Id, location.Id);
        var crime = new KillCrime
        {
            WorldId = worldId,
            PlayerId = creature.Id,
            LocationId = location.Id,
            VictimId = Guid.NewGuid(),
            VictimName = "Victim",
        };
        var crimeWitness = new CrimeWitness
        {
            WorldId = worldId,
            CrimeId = crime.Id,
            CreatureId = creature.Id,
        };
        var session = Builders.MakeGameSession(worldId, creature.Id);
        var chatMessage = new ChatMessage
        {
            SessionId = session.Id,
            Ordinal = 0,
            Role = "user",
            MessageJson = "{}",
        };
        var npcConversationSessionState = new NpcConversationSessionState
        {
            SessionId = session.Id,
            WorldId = worldId,
        };
        var creatureSpawner = Builders.MakeCreatureSpawner(worldId, location.Id);
        var restockPolicy = Builders.MakeRestockPolicy(worldId, Guid.NewGuid());
        var quest = Builders.MakeQuest(creature.Id, worldId: worldId);
        var questObjective = new ExploreLocationObjective
        {
            WorldId = worldId,
            QuestId = quest.Id,
            LocationId = location.Id,
        };
        var creatureQuest = Builders.MakeCreatureQuest(creature.Id, quest.Id, worldId: worldId);
        var creatureQuestObjective = Builders.MakeCreatureQuestObjective(
            creature.Id,
            questObjective.Id,
            worldId
        );
        var questReputationReward = new QuestReputationReward
        {
            QuestId = quest.Id,
            Score = 1,
            TargetId = faction.Id,
            TargetType = ReputationTargetType.Faction,
            WorldId = worldId,
        };
        var factDisclosureLockout = new FactDisclosureLockout
        {
            WorldId = worldId,
            PlayerId = creature.Id,
            NpcId = creature.Id,
            FactId = Guid.NewGuid(),
            Approach = FactDisclosureApproach.Bribe,
        };
        var fact = new Fact
        {
            WorldId = worldId,
            Subject = "Test",
            Value = "Fact",
        };
        var bookWork = new BookWork
        {
            WorldId = worldId,
            Title = "Test work",
            Tier = BookTier.Clue,
            SubjectType = BookSubjectType.CreatureType,
            SubjectName = "Test subject",
            PageCount = 1,
            FactId = fact.Id,
            FactPageNumber = 1,
        };
        var bookPage = new BookPage
        {
            WorldId = worldId,
            WorkId = bookWork.Id,
            PageNumber = 1,
            Text = "Test page",
        };
        var firstJob = Builders.MakeCreatureJob(
            creature.Id,
            locationId: location.Id,
            worldId: worldId
        );
        var secondJob = Builders.MakeCreatureJob(
            creature.Id,
            priority: 2,
            locationId: destination.Id,
            worldId: worldId
        );
        var circuit = new TravelCircuit { WorldId = worldId, Name = "Test circuit" };
        var journey = new Journey
        {
            WorldId = worldId,
            TravelCircuitId = circuit.Id,
            Status = JourneyStatus.Planned,
            PlannedAt = GameClock.Epoch,
            DepartureAt = GameClock.Epoch,
            CheckpointedAt = GameClock.Epoch,
        };
        var conversationHistory = new NpcConversationHistory
        {
            WorldId = worldId,
            CreatureId = creature.Id,
            NpcId = companion.Id,
        };

        _context.Worlds.Add(world);
        _context.Countries.Add(country);
        _context.States.Add(state);
        _context.Cities.Add(city);
        _context.Districts.Add(district);
        _context.Creatures.Add(creature);
        _context.Creatures.Add(companion);
        _context.Factions.Add(faction);
        _context.Factions.Add(rivalFaction);
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _context.Locations.Add(destination);
        _context.Props.Add(bed);
        _context.Items.Add(item);
        _context.LocationConnectors.AddRange(locationConnector, returnLocationConnector);
        _context.DoorConnectors.Add(doorConnector);
        var pointStart = Builders.MakeTravelNode(location.Id, worldId: worldId);
        var pointEnd = Builders.MakeTravelNode(location.Id, 5, 0, worldId);
        var exitNode = Builders.MakeExitNode(locationConnector);
        var arrivalNode = Builders.MakeArrivalNode(locationConnector);
        _context.TravelNodes.AddRange(exitNode, arrivalNode, pointStart, pointEnd);
        _context.PointConnectors.Add(
            Builders.MakePointConnector(location.Id, pointStart.Id, pointEnd.Id, 5, worldId)
        );
        _context.DoorConnectorKeys.Add(
            Builders.MakeDoorConnectorKey(item.Id, doorConnector.Id, worldId)
        );
        _context.DoorConnectorLevers.Add(
            Builders.MakeDoorConnectorLever(bed.Id, doorConnector.Id, worldId)
        );
        _context.RoomBookings.Add(
            Builders.MakeRoomBooking(worldId, room.Id, item.Id, creature.Id, GameClock.Epoch)
        );
        _context.BuildingOwners.Add(Builders.MakeBuildingOwner(building.Id, creature.Id, worldId));
        _context.FactionMembers.Add(factionMember);
        _context.FactionStandings.Add(
            new FactionStanding
            {
                WorldId = worldId,
                FactionId = faction.Id,
                OtherFactionId = rivalFaction.Id,
            }
        );
        _context.Relationships.Add(
            Builders.MakeRelationship(creature.Id, companion.Id, worldId: worldId)
        );
        _context.Reputations.Add(reputation);
        _context.ReputationLogEntries.Add(reputationLogEntry);
        _context.CreatureSkills.Add(Builders.MakeCreatureSkill(creature.Id, worldId: worldId));
        _context.CreatureWeaponProficiencies.Add(weaponProficiency);
        _context.CreatureProfiles.Add(
            Builders.MakeCreatureProfile(worldId, creature.Id, "Test profile")
        );
        _context.CreatureKnowledge.Add(
            new CreatureKnowledge
            {
                WorldId = worldId,
                KnowerId = creature.Id,
                SubjectId = fact.Id,
                SubjectType = KnowledgeSubjectType.Fact,
            }
        );
        _context.CreatureJobs.AddRange(firstJob, secondJob);
        _context.DungeonExpeditions.Add(Builders.MakeDungeonExpedition(creature, companion));
        _context.EncounterGroups.Add(encounterGroup);
        _context.EncounterGroupMembers.Add(encounterGroupMember);
        _context.Encounters.Add(encounter);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(crimeWitness);
        _context.GameSessions.Add(session);
        _context.ChatMessages.Add(chatMessage);
        _context.NpcConversationSessionStates.Add(npcConversationSessionState);
        _context.CreatureSpawners.Add(creatureSpawner);
        _context.RestockPolicies.Add(restockPolicy);
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(questObjective);
        _context.CreatureQuests.Add(creatureQuest);
        _context.CreatureQuestObjectives.Add(creatureQuestObjective);
        _context.QuestReputationRewards.Add(questReputationReward);
        _context.Facts.Add(fact);
        _context.BookWorks.Add(bookWork);
        _context.BookPages.Add(bookPage);
        _context.NpcConversationHistories.Add(conversationHistory);
        _context.NpcConversations.Add(
            new NpcConversation
            {
                WorldId = worldId,
                NpcConversationHistoryId = conversationHistory.Id,
                Summary = "Test conversation",
            }
        );
        _context.FactDisclosureLockouts.Add(factDisclosureLockout);
        _context.TravelCircuits.Add(circuit);
        _context.TravelCircuitLegs.AddRange(
            new TravelCircuitLeg
            {
                TravelCircuitId = circuit.Id,
                Index = 0,
                FromNodeId = exitNode.Id,
                ToNodeId = arrivalNode.Id,
                ConnectorId = locationConnector.Id,
            },
            new TravelCircuitLeg
            {
                TravelCircuitId = circuit.Id,
                Index = 1,
                FromNodeId = arrivalNode.Id,
                ToNodeId = exitNode.Id,
                ConnectorId = returnLocationConnector.Id,
            }
        );
        _context.Journeys.Add(journey);
        _context.JourneyLegs.Add(
            new JourneyLeg
            {
                JourneyId = journey.Id,
                Index = 0,
                FromNodeId = exitNode.Id,
                ToNodeId = arrivalNode.Id,
                ConnectorId = locationConnector.Id,
                Distance = locationConnector.Distance,
                Path = new Polyline(),
            }
        );
        _context.JourneyMembers.Add(
            new JourneyMember { JourneyId = journey.Id, CreatureId = creature.Id }
        );
        _context.WeatherStates.Add(
            new WeatherState { WorldId = worldId, StateId = Guid.NewGuid() }
        );
        _context.QuestSeedSchedules.Add(
            new QuestSeedSchedule { WorldId = worldId, LocationId = location.Id }
        );
        _context.QuestChainGenerationRequests.Add(
            new QuestChainGenerationRequest { WorldId = worldId, PlayerId = creature.Id }
        );
        _context.FactDisclosureAttempts.Add(
            new FactDisclosureAttempt
            {
                WorldId = worldId,
                PlayerId = factDisclosureLockout.PlayerId,
                NpcId = factDisclosureLockout.NpcId,
                FactId = factDisclosureLockout.FactId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _sessionIdByWorldId[worldId] = session.Id;
    }

    [Fact]
    public async Task Handle_DeletesEveryTableForTheWorld_AndLeavesOtherWorldsUntouched()
    {
        // Act
        await _handler.Handle(
            new DropWorldCommand { WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        await AssertWorldDataExists(WorldId, expected: false);
        await AssertWorldDataExists(OtherWorldId, expected: true);
    }

    private async Task AssertWorldDataExists(Guid worldId, bool expected)
    {
        await using var verifyContext = db.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(
            expected,
            await verifyContext.Worlds.AnyAsync(x => x.Id == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Creatures.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Factions.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Buildings.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Rooms.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Locations.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Props.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Items.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.FactionMembers.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.ReputationLogEntries.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.CreatureWeaponProficiencies.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.EncounterGroups.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.EncounterGroupMembers.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.Encounters.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.Crimes.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.CrimeWitnesses.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.GameSessions.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        var sessionId = _sessionIdByWorldId[worldId];
        Assert.Equal(
            expected,
            await verifyContext.ChatMessages.AnyAsync(
                x => x.SessionId == sessionId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.NpcConversationSessionStates.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.CreatureSpawners.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.RestockPolicies.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.Quests.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.QuestReputationRewards.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.FactDisclosureLockouts.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.FactDisclosureAttempts.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.TravelCircuits.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.Journeys.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.JourneyLegs.AnyAsync(
                leg =>
                    verifyContext.Journeys.Any(journey =>
                        journey.Id == leg.JourneyId && journey.WorldId == worldId
                    ),
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.JourneyMembers.AnyAsync(
                member =>
                    verifyContext.Journeys.Any(journey =>
                        journey.Id == member.JourneyId && journey.WorldId == worldId
                    ),
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.TravelCircuitLegs.AnyAsync(
                leg =>
                    verifyContext.TravelCircuits.Any(circuit =>
                        circuit.Id == leg.TravelCircuitId && circuit.WorldId == worldId
                    ),
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.WeatherStates.AnyAsync(x => x.WorldId == worldId, cancellationToken)
        );
        Assert.Equal(
            expected,
            await verifyContext.QuestSeedSchedules.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(
            expected,
            await verifyContext.QuestChainGenerationRequests.AnyAsync(
                x => x.WorldId == worldId,
                cancellationToken
            )
        );
        Assert.Equal(expected, await HasWorldData(verifyContext.BookPages, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.BookWorks, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.BuildingOwners, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.Cities, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.Countries, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.CreatureJobs, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.CreatureKnowledge, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.CreatureProfiles, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.CreatureQuestObjectives, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.CreatureQuests, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.CreatureSkills, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.Districts, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.DoorConnectorKeys, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.DoorConnectorLevers, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.DoorConnectors, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.DungeonExpeditions, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.FactionStandings, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.Facts, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.LocationConnectors, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.NpcConversationHistories, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.NpcConversations, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.PointConnectors, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.QuestObjectives, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.Relationships, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.Reputations, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.RoomBookings, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.States, worldId));
        Assert.Equal(expected, await HasWorldData(verifyContext.TravelNodes, worldId));
    }

    private static Task<bool> HasWorldData<TEntity>(DbSet<TEntity> entities, Guid worldId)
        where TEntity : class =>
        entities.AnyAsync(
            entity => EF.Property<Guid>(entity, nameof(Creature.WorldId)) == worldId,
            TestContext.Current.CancellationToken
        );
}
