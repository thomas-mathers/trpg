using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TRPG.Data.Models;

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
    public DbSet<NpcConversation> NpcConversations => Set<NpcConversation>();
    public DbSet<Prop> Props => Set<Prop>();
    public DbSet<QuestObjective> QuestObjectives => Set<QuestObjective>();
    public DbSet<Quest> Quests => Set<Quest>();
    public DbSet<Relationship> Relationships => Set<Relationship>();
    public DbSet<Reputation> Reputations => Set<Reputation>();
    public DbSet<Road> Roads => Set<Road>();
    public DbSet<LocationConnectorKey> LocationConnectorKeys => Set<LocationConnectorKey>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<State> States => Set<State>();
    public DbSet<WorldEvent> WorldEvents => Set<WorldEvent>();
    public DbSet<World> Worlds => Set<World>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Fight> Fights => Set<Fight>();

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

        modelBuilder.Entity<Creature>(entity =>
        {
            entity.HasIndex(p => p.WorldId);
            entity.HasIndex(p => p.LocationId);
            entity.OwnsOne(p => p.BaseAttributes, s => s.ToJson());
            entity.Property(c => c.ActiveConditions).HasJsonConversion(() => []);
            entity.Property(c => c.CooldownRemainingByAbility).HasJsonConversion(() => []);
            entity.Property(c => c.ActiveDots).HasJsonConversion(() => []);
            entity.Property(c => c.ActiveHots).HasJsonConversion(() => []);
            entity.Property(c => c.ActiveBuffs).HasJsonConversion(() => []);
        });

        modelBuilder.Entity<Fight>(entity =>
        {
            entity.HasIndex(f => f.WorldId);
            entity.HasIndex(f => f.PlayerId);
            entity.Property(f => f.CombatantIds).HasColumnType("uuid[]");
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

        modelBuilder.Entity<LocationConnectorKey>(entity =>
        {
            entity.HasIndex(k => k.LocationConnectorId);
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
            entity.HasIndex(l => l.RoomId).IsUnique();
            entity.HasIndex(l => l.DistrictId).IsUnique().HasFilter("room_id IS NULL");
        });

        modelBuilder.Entity<Prop>(entity =>
        {
            entity
                .HasDiscriminator<string>("behavior_type")
                .HasValue<Seat>("Seat")
                .HasValue<Workstation>("Workstation")
                .HasValue<Bed>("Bed")
                .HasValue<Container>("Container")
                .HasValue<LocationConnector>("LocationConnector")
                .HasValue<Trigger>("Trigger");
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
                .HasValue<ExploreBuildingObjective>("ExploreBuilding")
                .HasValue<ExploreCityObjective>("ExploreCity")
                .HasValue<SpeakToCreatureObjective>("SpeakToCreature");
            entity.HasIndex(o => o.QuestId);
            entity.HasIndex(o => o.WorldId);
        });

        modelBuilder.Entity<KillCreatureTypeObjective>(entity =>
        {
            entity
                .Property(o => o.RequiredAmount)
                .HasColumnName("kill_creature_type_required_amount");
        });

        modelBuilder.Entity<CollectItemObjective>(entity =>
        {
            entity.Property(o => o.RequiredAmount).HasColumnName("collect_item_required_amount");
        });

        modelBuilder.Entity<CreatureSkill>(entity =>
        {
            entity.HasIndex(ps => new { ps.CreatureId, ps.Skill }).IsUnique();
            entity.HasIndex(ps => ps.WorldId);
        });

        modelBuilder.Entity<CreatureQuest>(entity =>
        {
            entity.HasOne(pq => pq.Quest).WithMany().HasForeignKey(pq => pq.QuestId);
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
            entity.HasIndex(b => new { b.StateId, b.Name }).IsUnique();
            entity.HasIndex(b => b.WorldId);
        });

        modelBuilder.Entity<Road>(entity =>
        {
            entity.HasIndex(r => new { r.OriginStateId, r.DestinationStateId }).IsUnique();
            entity.HasIndex(r => r.WorldId);
        });

        modelBuilder.Entity<NpcConversation>(entity =>
        {
            entity.HasIndex(c => c.WorldId);
            entity.HasIndex(c => new { c.NpcId, c.CreatureId }).IsUnique();
        });

        modelBuilder.Entity<WorldEvent>().HasIndex(e => e.WorldId);

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
