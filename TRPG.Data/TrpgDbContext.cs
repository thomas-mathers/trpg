using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TRPG.Data.Models;

namespace TRPG.Data;

public class TrpgDbContext(DbContextOptions<TrpgDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        AllowOutOfOrderMetadataProperties = true,
    };

    public DbSet<BuildingOwner> BuildingOwners => Set<BuildingOwner>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<ContainerItem> ContainerItems => Set<ContainerItem>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<CreatureAbility> CreatureAbilities => Set<CreatureAbility>();
    public DbSet<CreatureKnowledge> CreatureKnowledge => Set<CreatureKnowledge>();
    public DbSet<CreatureQuestObjective> CreatureQuestObjectives => Set<CreatureQuestObjective>();
    public DbSet<CreatureQuest> CreatureQuests => Set<CreatureQuest>();
    public DbSet<Creature> Creatures => Set<Creature>();
    public DbSet<CreatureSkill> CreatureSkills => Set<CreatureSkill>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<FactionMember> FactionMembers => Set<FactionMember>();
    public DbSet<Faction> Factions => Set<Faction>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<NpcConversation> NpcConversations => Set<NpcConversation>();
    public DbSet<Prop> Props => Set<Prop>();
    public DbSet<QuestObjective> QuestObjectives => Set<QuestObjective>();
    public DbSet<Quest> Quests => Set<Quest>();
    public DbSet<Relationship> Relationships => Set<Relationship>();
    public DbSet<Reputation> Reputations => Set<Reputation>();
    public DbSet<Road> Roads => Set<Road>();
    public DbSet<RoomConnectorKey> RoomConnectorKeys => Set<RoomConnectorKey>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<State> States => Set<State>();
    public DbSet<WorldEvent> WorldEvents => Set<WorldEvent>();
    public DbSet<World> Worlds => Set<World>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<AmountType>().HaveConversion<string>();
        configurationBuilder.Properties<BuildingType>().HaveConversion<string>();
        configurationBuilder.Properties<WorkstationType>().HaveConversion<string>();
        configurationBuilder.Properties<AttributeName>().HaveConversion<string>();
        configurationBuilder.Properties<ConditionType>().HaveConversion<string>();
        configurationBuilder.Properties<DamageType>().HaveConversion<string>();
        configurationBuilder.Properties<EquipmentSlot>().HaveConversion<string>();
        configurationBuilder.Properties<TargetType>().HaveConversion<string>();
        configurationBuilder.Properties<WeaponType>().HaveConversion<string>();
        configurationBuilder.Properties<ArmorClass>().HaveConversion<string>();
        configurationBuilder.Properties<ArmorType>().HaveConversion<string>();
        configurationBuilder.Properties<AccessoryType>().HaveConversion<string>();
        configurationBuilder.Properties<AmmoType>().HaveConversion<string>();
        configurationBuilder.Properties<ItemRarity>().HaveConversion<string>();
        configurationBuilder.Properties<FactionRole>().HaveConversion<string>();
        configurationBuilder.Properties<ReputationTargetType>().HaveConversion<string>();
        configurationBuilder.Properties<QuestStatus>().HaveConversion<string>();
        configurationBuilder.Properties<QuestObjectiveType>().HaveConversion<string>();
        configurationBuilder.Properties<JobAction>().HaveConversion<string>();
        configurationBuilder.Properties<Profession>().HaveConversion<string>();
        configurationBuilder.Properties<CreatureState>().HaveConversion<string>();
        configurationBuilder.Properties<CreatureType>().HaveConversion<string>();
        configurationBuilder.Properties<Gender>().HaveConversion<string>();
        configurationBuilder.Properties<DistrictType>().HaveConversion<string>();
        configurationBuilder.Properties<Skill>().HaveConversion<string>();
        configurationBuilder.Properties<QuestTargetType>().HaveConversion<string>();
        configurationBuilder.Properties<RelationshipType>().HaveConversion<string>();
        configurationBuilder.Properties<DayOfWeek>().HaveConversion<string>();
        configurationBuilder.Properties<KnowledgeSubjectType>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<CreatureKnowledge>(entity =>
        {
            entity.HasIndex(k => new { k.KnowerId, k.SubjectType });
            entity.HasIndex(k => k.WorldId);
        });

        modelBuilder.Entity<Creature>(entity =>
        {
            entity.HasIndex(p => p.WorldId);
            entity.HasIndex(p => new { p.StateId, p.RoomId });
            entity.OwnsOne(p => p.Attributes, s => s.ToJson());
            entity
                .Property(p => p.ActiveConditions)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonOptions),
                    v =>
                        JsonSerializer.Deserialize<Dictionary<ConditionType, int>>(v, JsonOptions)
                        ?? new Dictionary<ConditionType, int>()
                )
                .HasColumnType("jsonb");
            entity.OwnsMany(p => p.ActiveModifiers, m => m.ToJson());
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity
                .HasDiscriminator<string>("item_type")
                .HasValue<Item>("generic")
                .HasValue<WeaponItem>("weapon")
                .HasValue<ShieldItem>("shield")
                .HasValue<ArmorItem>("armor")
                .HasValue<ConsumableItem>("consumable")
                .HasValue<AmmunitionItem>("ammunition")
                .HasValue<AccessoryItem>("accessory");
            entity
                .Property(i => i.Modifiers)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonOptions),
                    v =>
                        JsonSerializer.Deserialize<List<ItemModifier>>(v, JsonOptions)
                        ?? new List<ItemModifier>()
                )
                .HasColumnType("jsonb");
            entity.HasIndex(i => i.WorldId);
        });

        modelBuilder.Entity<WeaponItem>().Property(w => w.Type).HasColumnName("weapon_type");
        modelBuilder.Entity<ArmorItem>().Property(a => a.Type).HasColumnName("armor_type");
        modelBuilder.Entity<AmmunitionItem>().Property(a => a.Type).HasColumnName("ammo_type");
        modelBuilder.Entity<AccessoryItem>().Property(a => a.Type).HasColumnName("accessory_type");

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasOne(i => i.Item).WithMany().HasForeignKey(i => i.ItemId);
            entity.HasIndex(i => i.CreatureId);
            entity.HasIndex(i => i.WorldId);
        });

        modelBuilder.Entity<RoomConnectorKey>(entity =>
        {
            entity.HasIndex(k => k.RoomConnectorId);
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
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasIndex(r => r.BuildingId);
            entity.HasIndex(r => r.WorldId);
        });

        modelBuilder.Entity<Prop>(entity =>
        {
            entity
                .HasDiscriminator<string>("behavior_type")
                .HasValue<Seat>("Seat")
                .HasValue<Workstation>("Workstation")
                .HasValue<Bed>("Bed")
                .HasValue<Container>("Container")
                .HasValue<RoomConnector>("RoomConnector")
                .HasValue<Trigger>("Trigger");
            entity.HasIndex(p => p.RoomId);
            entity.HasIndex(p => p.WorldId);
        });

        modelBuilder.Entity<Container>(entity =>
        {
            entity.HasMany(s => s.Items).WithOne().HasForeignKey(pi => pi.ContainerId);
        });

        modelBuilder.Entity<ContainerItem>(entity =>
        {
            entity.HasIndex(pi => new { pi.ContainerId, pi.Index }).IsUnique();
            entity.HasIndex(pi => pi.WorldId);
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
            entity.HasIndex(o => o.QuestId);
            entity.HasIndex(o => o.WorldId);
        });

        modelBuilder.Entity<CreatureAbility>(entity =>
        {
            entity.HasIndex(pa => new { pa.CreatureId, pa.AbilityName }).IsUnique();
            entity.HasIndex(pa => pa.WorldId);
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

        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasIndex(j => new { j.StateId, j.RoomId });
            entity.HasIndex(j => j.CreatureId);
            entity.HasIndex(j => j.RoomId);
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
    }
}
