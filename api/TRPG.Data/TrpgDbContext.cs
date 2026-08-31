using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TRPG.Domain.Models;

namespace TRPG.Data;

file static class JsonColumnConversion
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        AllowOutOfOrderMetadataProperties = true,
    };

    public static PropertyBuilder<T> HasJsonConversion<T>(
        this PropertyBuilder<T> propertyBuilder,
        Func<T> defaultValue
    ) =>
        propertyBuilder
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<T>(v, JsonOptions) ?? defaultValue(),
                CreateValueComparer<T>()
            )
            .HasColumnType("jsonb");

    private static ValueComparer<T> CreateValueComparer<T>() =>
        new(
            (left, right) =>
                JsonSerializer.Serialize(left, JsonOptions)
                == JsonSerializer.Serialize(right, JsonOptions),
            value =>
                JsonSerializer.Serialize(value, JsonOptions).GetHashCode(StringComparison.Ordinal),
            value =>
                JsonSerializer.Deserialize<T>(
                    JsonSerializer.Serialize(value, JsonOptions),
                    JsonOptions
                )!
        );
}

public class TrpgDbContext(DbContextOptions<TrpgDbContext> options) : DbContext(options)
{
    public DbSet<BuildingOwner> BuildingOwners => Set<BuildingOwner>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<CreatureKnowledge> CreatureKnowledge => Set<CreatureKnowledge>();
    public DbSet<CreatureWeaponProficiency> CreatureWeaponProficiencies =>
        Set<CreatureWeaponProficiency>();
    public DbSet<CreatureQuestObjective> CreatureQuestObjectives => Set<CreatureQuestObjective>();
    public DbSet<CreatureQuest> CreatureQuests => Set<CreatureQuest>();
    public DbSet<Creature> Creatures => Set<Creature>();
    public DbSet<CreatureSkill> CreatureSkills => Set<CreatureSkill>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<FactionMember> FactionMembers => Set<FactionMember>();
    public DbSet<Faction> Factions => Set<Faction>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<CreatureJob> CreatureJobs => Set<CreatureJob>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<LocationConnector> LocationConnectors => Set<LocationConnector>();
    public DbSet<DoorConnector> DoorConnectors => Set<DoorConnector>();
    public DbSet<NpcConversation> NpcConversations => Set<NpcConversation>();
    public DbSet<NpcConversationHistory> NpcConversationHistories => Set<NpcConversationHistory>();
    public DbSet<NpcProfile> NpcProfiles => Set<NpcProfile>();
    public DbSet<Prop> Props => Set<Prop>();
    public DbSet<QuestObjective> QuestObjectives => Set<QuestObjective>();
    public DbSet<Quest> Quests => Set<Quest>();
    public DbSet<QuestReputationReward> QuestReputationRewards => Set<QuestReputationReward>();
    public DbSet<Relationship> Relationships => Set<Relationship>();
    public DbSet<Reputation> Reputations => Set<Reputation>();
    public DbSet<ReputationLogEntry> ReputationLogEntries => Set<ReputationLogEntry>();
    public DbSet<DoorConnectorKey> DoorConnectorKeys => Set<DoorConnectorKey>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<EncounterGroup> EncounterGroups => Set<EncounterGroup>();
    public DbSet<EncounterGroupMember> EncounterGroupMembers => Set<EncounterGroupMember>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<State> States => Set<State>();
    public DbSet<TravelConnector> TravelConnectors => Set<TravelConnector>();
    public DbSet<WorldEvent> WorldEvents => Set<WorldEvent>();
    public DbSet<World> Worlds => Set<World>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<NpcConversationSessionState> NpcConversationSessionStates =>
        Set<NpcConversationSessionState>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Crime> Crimes => Set<Crime>();
    public DbSet<CrimeWitness> CrimeWitnesses => Set<CrimeWitness>();
    public DbSet<CreatureSpawner> CreatureSpawners => Set<CreatureSpawner>();
    public DbSet<RestockPolicy> RestockPolicies => Set<RestockPolicy>();
    public DbSet<RoomBooking> RoomBookings => Set<RoomBooking>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<CreatureWeaponProficiency>(entity =>
        {
            entity.HasKey(k => new { k.CreatureId, k.WeaponType });
            entity.HasIndex(k => new { k.WorldId });
        });

        modelBuilder.Entity<CreatureKnowledge>(entity =>
        {
            entity.HasIndex(k => new { k.KnowerId, k.SubjectType });
            entity.HasIndex(k => k.WorldId);
        });

        modelBuilder.Entity<Crime>(entity =>
        {
            entity.HasIndex(c => c.WorldId);
            entity.HasIndex(c => new
            {
                c.WorldId,
                c.PlayerId,
                c.LocationId,
            });
            entity
                .HasDiscriminator<string>("crime_type")
                .HasValue<KillCrime>("Kill")
                .HasValue<TheftCrime>("Theft");
        });

        modelBuilder.Entity<CrimeWitness>(entity =>
        {
            entity.HasIndex(w => w.WorldId);
            entity.HasIndex(w => new { w.CrimeId, w.CreatureId }).IsUnique();
            entity.HasIndex(w => new { w.WorldId, w.CreatureId });
        });

        modelBuilder.Entity<TheftCrime>(entity =>
        {
            entity.Property(crime => crime.Items).HasJsonConversion(() => []);
        });

        modelBuilder.Entity<Creature>(entity =>
        {
            entity.HasIndex(p => p.WorldId);
            entity.HasIndex(p => p.LocationId);
            entity.HasIndex(p => p.SpawnerId);
            entity.OwnsOne(p => p.BaseAttributes, s => s.ToJson());
            entity.Property(c => c.ActiveConditions).HasJsonConversion(() => []);
            entity.Property(c => c.CooldownRemainingByAbility).HasJsonConversion(() => []);
            entity.Property(c => c.ActiveDots).HasJsonConversion(() => []);
            entity.Property(c => c.ActiveHots).HasJsonConversion(() => []);
            entity.Property(c => c.ActiveBuffs).HasJsonConversion(() => []);
        });

        modelBuilder.Entity<Encounter>(entity =>
        {
            entity.HasIndex(e => e.WorldId);
            entity.HasIndex(e => new { e.PlayerId, e.State });
            entity
                .HasDiscriminator<string>("encounter_type")
                .HasValue<HostileEncounter>("Hostile")
                .HasValue<FightEncounter>("Fight")
                .HasValue<GuardEncounter>("Guard")
                .HasValue<TheftEncounter>("Theft");
        });

        modelBuilder.Entity<HostileEncounter>(entity =>
        {
            entity.Property(e => e.Members).HasJsonConversion(() => []);
        });

        modelBuilder.Entity<FightEncounter>(entity =>
        {
            entity.Property(e => e.CombatantIds).HasColumnType("uuid[]");
        });

        modelBuilder.Entity<GuardEncounter>(entity =>
        {
            entity.Property(e => e.RecentOffenses).HasColumnType("text[]");
        });

        modelBuilder.Entity<TheftEncounter>(entity =>
        {
            entity.Property(e => e.ItemIds).HasColumnType("uuid[]");
            entity.Property(e => e.ItemNames).HasColumnType("text[]");
            entity.Property(e => e.ItemSelections).HasJsonConversion(() => []);
            entity.Property(e => e.WitnessCreatureIds).HasColumnType("uuid[]");
        });

        modelBuilder.Entity<EncounterGroup>(entity =>
        {
            entity.HasIndex(g => g.WorldId);
            entity.HasIndex(g => new { g.WorldId, g.LocationId });
        });

        modelBuilder.Entity<EncounterGroupMember>(entity =>
        {
            entity.HasKey(m => new { m.EncounterGroupId, m.CreatureId });
            entity.HasIndex(m => m.CreatureId).IsUnique();
            entity.HasIndex(m => m.WorldId);
        });

        modelBuilder
            .Entity<Faction>()
            .Property(f => f.Temperament)
            .HasDefaultValue(FactionTemperament.Authoritative);

        modelBuilder.Entity<Faction>(entity =>
        {
            entity.HasIndex(f => new { f.WorldId, f.CreatureType });
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity
                .HasDiscriminator<string>("item_type")
                .HasValue<Item>("generic")
                .HasValue<Weapon>("weapon")
                .HasValue<Shield>("shield")
                .HasValue<Armor>("armor")
                .HasValue<Consumable>("consumable")
                .HasValue<Ammunition>("ammunition")
                .HasValue<Accessory>("accessory")
                .HasValue<Gold>("gold")
                .HasValue<Key>("key");
            entity.Property(i => i.Modifiers).HasJsonConversion(() => []);
            entity.Property(i => i.GoldValue).HasColumnName("gold_value");
            entity.HasIndex(i => i.WorldId);
            entity.OwnsOne(
                i => i.Ownership,
                ownership =>
                {
                    ownership.HasIndex(o => o.OwnerId).HasDatabaseName("ix_items_owner_id");
                    ownership
                        .HasIndex(o => new { o.OwnerId, o.EquippedSlot })
                        .IsUnique()
                        .HasDatabaseName("ux_items_owner_equipped_slot")
                        .HasFilter("ownership_equipped_slot IS NOT NULL");
                    ownership
                        .HasIndex(o => o.OwnerId)
                        .IsUnique()
                        .HasDatabaseName("ux_items_gold_owner")
                        .HasFilter("item_type = 'gold'");
                }
            );
        });

        foreach (
            var tradeableItemType in new[]
            {
                typeof(Weapon),
                typeof(Armor),
                typeof(Shield),
                typeof(Accessory),
                typeof(Consumable),
                typeof(Ammunition),
            }
        )
        {
            var entity = modelBuilder.Entity(tradeableItemType);
            entity.Property("Level").HasColumnName("level");
            entity.Property("Rarity").HasColumnName("rarity");
        }

        modelBuilder.Entity<Weapon>().Property(w => w.Type).HasColumnName("weapon_type");
        modelBuilder.Entity<Armor>().Property(a => a.Type).HasColumnName("armor_type");
        modelBuilder.Entity<Ammunition>().Property(a => a.Type).HasColumnName("ammo_type");
        modelBuilder.Entity<Accessory>().Property(a => a.Type).HasColumnName("accessory_type");

        modelBuilder.Entity<DoorConnectorKey>(entity =>
        {
            entity.HasIndex(k => k.DoorConnectorId);
            entity.HasIndex(k => k.ItemId);
            entity.HasIndex(k => k.WorldId);
        });

        modelBuilder.Entity<WorldEvent>(entity =>
        {
            entity.Property(e => e.Tags).HasColumnType("text[]");
        });

        modelBuilder.Entity<World>(entity =>
        {
            entity.OwnsOne(w => w.Boundary, b => b.ToJson());
        });

        modelBuilder.Entity<Country>(entity =>
        {
            entity.OwnsOne(
                c => c.Boundary,
                b =>
                {
                    b.ToJson();
                    b.OwnsMany(p => p.Points);
                }
            );
        });

        modelBuilder.Entity<State>(entity =>
        {
            entity.OwnsOne(
                s => s.Boundary,
                b =>
                {
                    b.ToJson();
                    b.OwnsMany(p => p.Points);
                }
            );
            entity.OwnsOne(s => s.Center, c => c.ToJson());
        });

        modelBuilder.Entity<City>(entity =>
        {
            entity.HasIndex(c => c.StateId).IsUnique();
            entity.HasIndex(c => new { c.CountryId, c.Name }).IsUnique();
            entity.HasIndex(c => c.WorldId);
        });

        modelBuilder.Entity<District>(entity =>
        {
            entity.HasIndex(d => d.CityId);
            entity.HasIndex(d => new { d.CityId, d.DistrictType }).IsUnique();
            entity.HasIndex(d => d.WorldId);
            entity.HasIndex(d => d.LocationId).IsUnique();
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasIndex(r => r.BuildingId);
            entity.HasIndex(r => r.WorldId);
            entity.HasIndex(r => r.LocationId).IsUnique();
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasIndex(l => l.WorldId);
            entity.HasIndex(l => l.StateId);
            entity.HasIndex(l => l.RoomId).IsUnique();
            entity.HasIndex(l => l.DistrictId).IsUnique().HasFilter("room_id IS NULL");
        });

        modelBuilder.Entity<LocationConnector>(entity =>
        {
            entity.HasIndex(c => c.OriginLocationId);
            entity.HasIndex(c => c.DestinationLocationId);
            entity.HasIndex(c => new { c.OriginLocationId, c.DestinationLocationId });
            entity.HasIndex(c => c.WorldId);
        });

        modelBuilder.Entity<DoorConnector>(entity =>
        {
            entity.HasIndex(c => c.ConnectorId).IsUnique();
            entity.HasIndex(c => c.WorldId);
        });

        modelBuilder.Entity<TravelConnector>(entity =>
        {
            entity.HasIndex(c => c.ConnectorId).IsUnique();
            entity.HasIndex(c => c.WorldId);
        });

        modelBuilder.Entity<Prop>(entity =>
        {
            entity
                .HasDiscriminator<string>("behavior_type")
                .HasValue<Seat>("Seat")
                .HasValue<Workstation>("Workstation")
                .HasValue<Bed>("Bed")
                .HasValue<Container>("Container")
                .HasValue<Trigger>("Trigger");
            entity.Property<string>("behavior_type").HasColumnType("text");
            entity.HasIndex(p => p.LocationId);
            entity.HasIndex(p => p.WorldId);
        });

        modelBuilder.Entity<Quest>(entity =>
        {
            entity.Property(q => q.ItemRewards).HasColumnType("uuid[]");
            entity.Property(q => q.PrerequisiteQuestIds).HasColumnType("uuid[]");
            entity.HasIndex(q => q.WorldId);
            entity.HasIndex(q => q.GiverId);
        });

        modelBuilder.Entity<QuestObjective>(entity =>
        {
            entity
                .HasDiscriminator<string>("objective_kind")
                .HasValue<KillCreatureObjective>("KillCreature")
                .HasValue<KillCreatureTypeObjective>("KillCreatureType")
                .HasValue<CollectItemObjective>("CollectItem")
                .HasValue<ExploreLocationObjective>("ExploreLocation")
                .HasValue<SpeakToCreatureObjective>("SpeakToCreature");
            entity.HasIndex(o => o.QuestId);
            entity.HasIndex(o => o.WorldId);
            entity.Property(o => o.RequiredAmount).HasDefaultValue(1);
        });

        modelBuilder.Entity<CreatureSkill>(entity =>
        {
            entity.HasIndex(ps => new { ps.CreatureId, ps.Skill }).IsUnique();
            entity.HasIndex(ps => ps.WorldId);
        });

        modelBuilder.Entity<CreatureQuest>(entity =>
        {
            entity.HasOne(pq => pq.Quest).WithMany().HasForeignKey(pq => pq.QuestId);
            entity.Property(pq => pq.IsTracked).HasDefaultValue(true);
            entity.HasIndex(pq => new { pq.CreatureId, pq.QuestId }).IsUnique();
            entity.HasIndex(pq => pq.WorldId);
        });

        modelBuilder.Entity<CreatureQuestObjective>(entity =>
        {
            entity.HasOne(po => po.Objective).WithMany().HasForeignKey(po => po.ObjectiveId);
            entity.HasIndex(po => new { po.CreatureId, po.ObjectiveId }).IsUnique();
            entity.HasIndex(po => po.WorldId);
        });

        modelBuilder.Entity<CreatureJob>(entity =>
        {
            entity.HasIndex(j => j.CreatureId);
            entity.HasIndex(j => j.LocationId);
            entity.HasIndex(j => j.WorldId);
        });

        modelBuilder.Entity<CreatureSpawner>(entity =>
        {
            entity.HasIndex(s => s.WorldId);
            entity.HasIndex(s => s.LocationId);
            entity.Property(s => s.ArchetypeCreatureTypes).HasJsonConversion(() => []);
        });

        modelBuilder.Entity<RestockPolicy>(entity =>
        {
            entity.HasIndex(p => p.WorldId);
            entity.HasIndex(p => p.WorkstationId).IsUnique();
        });

        modelBuilder.Entity<RoomBooking>(entity =>
        {
            entity.HasIndex(b => b.WorldId);
            entity.HasIndex(b => b.RoomId);
            entity.HasIndex(b => b.PlayerId);
        });

        modelBuilder.Entity<World>().HasIndex(w => w.Name).IsUnique();

        modelBuilder.Entity<Faction>().HasIndex(f => new { f.WorldId, f.Name }).IsUnique();

        modelBuilder.Entity<Country>().HasIndex(c => new { c.WorldId, c.Name }).IsUnique();

        modelBuilder.Entity<State>(entity =>
        {
            entity.HasIndex(s => new { s.CountryId, s.Name }).IsUnique();
            entity.HasIndex(s => s.WorldId);
        });

        modelBuilder.Entity<Building>(entity =>
        {
            entity.HasIndex(b => b.ExteriorLocationId);
            entity.HasIndex(b => b.WorldId);
        });

        modelBuilder.Entity<NpcConversationHistory>(entity =>
        {
            entity.HasIndex(c => c.WorldId);
            entity.HasIndex(c => new { c.NpcId, c.CreatureId }).IsUnique();
            entity.Property(c => c.DurableFacts).HasJsonConversion(() => []);
            entity.Property(c => c.OpenThreads).HasJsonConversion(() => []);
        });

        modelBuilder.Entity<NpcConversation>(entity =>
        {
            entity.HasIndex(c => c.WorldId);
            entity.HasIndex(c => new { c.NpcConversationHistoryId, c.CreatedAt });
        });

        modelBuilder.Entity<NpcProfile>(entity =>
        {
            entity.HasIndex(profile => profile.WorldId);
            entity.HasIndex(profile => profile.CreatureId).IsUnique();
            entity
                .Property(profile => profile.Appearance)
                .HasJsonConversion(() => new NpcAppearance());
            entity.Property(profile => profile.Behavior).HasJsonConversion(() => new NpcBehavior());
            entity
                .Property(profile => profile.PrivateBackground)
                .HasJsonConversion(() => new NpcPrivateBackground());
        });

        modelBuilder.Entity<WorldEvent>(entity =>
        {
            entity.HasIndex(e => e.WorldId);
            entity.HasIndex(e => e.LocationId);
        });

        modelBuilder.Entity<Reputation>(entity =>
        {
            entity
                .HasIndex(r => new
                {
                    r.CreatureId,
                    r.TargetId,
                    r.TargetType,
                })
                .IsUnique();
            entity.HasIndex(r => r.WorldId);
            entity.ToTable(table =>
                table.HasCheckConstraint("ck_reputations_score", "score BETWEEN -100 AND 100")
            );
        });

        modelBuilder.Entity<ReputationLogEntry>(entity =>
        {
            entity.HasIndex(r => new
            {
                r.CreatureId,
                r.TargetId,
                r.TargetType,
                r.CreatedAt,
            });
            entity.HasIndex(r => r.WorldId);
        });

        modelBuilder.Entity<QuestReputationReward>(entity =>
        {
            entity.HasIndex(reward => reward.WorldId);
            entity.HasIndex(reward => reward.QuestId);
        });

        modelBuilder.Entity<FactionMember>(entity =>
        {
            entity.HasIndex(fm => new { fm.CreatureId, fm.FactionId }).IsUnique();
            entity.HasIndex(fm => fm.FactionId);
            entity.HasIndex(fm => fm.WorldId);
        });

        modelBuilder.Entity<BuildingOwner>(entity =>
        {
            entity.HasIndex(bo => new { bo.BuildingId, bo.OwnerId }).IsUnique();
            entity.HasIndex(bo => bo.OwnerId);
            entity.HasIndex(bo => bo.WorldId);
        });

        modelBuilder.Entity<Relationship>(entity =>
        {
            entity
                .HasIndex(r => new
                {
                    r.SubjectId,
                    r.RelativeId,
                    r.RelationshipType,
                })
                .IsUnique();
            entity.HasIndex(r => r.RelativeId);
            entity.HasIndex(r => r.WorldId);
        });

        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.HasIndex(s => s.WorldId);
        });

        modelBuilder.Entity<NpcConversationSessionState>(entity =>
        {
            entity.HasKey(s => s.SessionId);
            entity.HasIndex(s => s.WorldId);
            entity.Property(s => s.OpenConversationCreatureIdsByName).HasJsonConversion(() => []);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasIndex(m => new { m.SessionId, m.Ordinal }).IsUnique();
            entity.HasIndex(m => new { m.SessionId, m.Role });
            entity.Property(m => m.MessageJson).HasColumnType("json");
        });
    }
}
