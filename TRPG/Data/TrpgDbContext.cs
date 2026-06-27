using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TRPG.Models;

namespace TRPG.Data;

internal class TrpgDbContext(DbContextOptions<TrpgDbContext> options) : DbContext(options) {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        Converters = { new JsonStringEnumConverter() }
    };

    public DbSet<BuildingOwner> BuildingOwners => Set<BuildingOwner>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<PropItem> PropItems => Set<PropItem>();
    public DbSet<Prop> Props => Set<Prop>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<FactionMember> FactionMembers => Set<FactionMember>();
    public DbSet<Faction> Factions => Set<Faction>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<NpcChatMessage> NpcChatMessages => Set<NpcChatMessage>();
    public DbSet<NpcConversation> NpcConversations => Set<NpcConversation>();
    public DbSet<PersonQuestObjective> PersonQuestObjectives => Set<PersonQuestObjective>();
    public DbSet<PersonQuest> PersonQuests => Set<PersonQuest>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<PersonSkill> PersonSkills => Set<PersonSkill>();
    public DbSet<Profession> Professions => Set<Profession>();
    public DbSet<QuestObjective> QuestObjectives => Set<QuestObjective>();
    public DbSet<Quest> Quests => Set<Quest>();
    public DbSet<Race> Races => Set<Race>();
    public DbSet<Reputation> Reputations => Set<Reputation>();
    public DbSet<SkillPrerequisite> SkillPrerequisites => Set<SkillPrerequisite>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Road> Roads => Set<Road>();
    public DbSet<WorldEvent> WorldEvents => Set<WorldEvent>();
    public DbSet<World> Worlds => Set<World>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) {
        configurationBuilder.Properties<AmountType>().HaveConversion<string>();
        configurationBuilder.Properties<BuildingType>().HaveConversion<string>();
        configurationBuilder.Properties<AttributeName>().HaveConversion<string>();
        configurationBuilder.Properties<ConditionType>().HaveConversion<string>();
        configurationBuilder.Properties<DamageType>().HaveConversion<string>();
        configurationBuilder.Properties<EquipmentSlot>().HaveConversion<string>();
        configurationBuilder.Properties<TargetType>().HaveConversion<string>();
        configurationBuilder.Properties<ItemCategory>().HaveConversion<string>();
        configurationBuilder.Properties<FactionRole>().HaveConversion<string>();
        configurationBuilder.Properties<QuestStatus>().HaveConversion<string>();
        configurationBuilder.Properties<QuestObjectiveType>().HaveConversion<string>();
        configurationBuilder.Properties<JobAction>().HaveConversion<string>();
        configurationBuilder.Properties<QuestTargetType>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<Person>(entity => {
            entity.HasIndex(p => p.WorldId);
            entity.OwnsOne(p => p.Location, lo => {
                lo.OwnsOne(l => l.Coordinates);
                lo.HasIndex(l => new { l.CityId, l.BuildingId });
            });
            entity.OwnsOne(p => p.Progression, prog => {
                prog.ToJson();
                prog.OwnsOne(p => p.Experience);
            });
            entity.OwnsOne(p => p.Attributes, s => {
                s.ToJson();
                s.OwnsOne(st => st.Hp);
                s.OwnsOne(st => st.Ap);
            });
            entity.Property(p => p.ActiveConditions)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonOptions),
                    v => JsonSerializer.Deserialize<Dictionary<ConditionType, int>>(v, JsonOptions) ?? new()
                )
                .HasColumnType("jsonb");
            entity.OwnsMany(p => p.ActiveModifiers, m => m.ToJson());
        });

        modelBuilder.Entity<Item>(entity => {
            entity.OwnsMany(i => i.Modifiers, m => m.ToJson());
            entity.HasIndex(i => new { i.WorldId, i.Name }).IsUnique();
        });

        modelBuilder.Entity<InventoryItem>(entity => {
            entity.HasOne(i => i.Item).WithMany().HasForeignKey(i => i.ItemId);
            entity.HasIndex(i => i.PersonId);
        });

        modelBuilder.Entity<Skill>(entity => {
            entity.HasDiscriminator<string>("skill_type")
                  .HasValue<Attack>("Attack")
                  .HasValue<Support>("Support");
            entity.HasIndex(s => new { s.WorldId, s.Name }).IsUnique();
        });

        modelBuilder.Entity<Attack>(entity => {
            entity.OwnsMany(a => a.Conditions, c => c.ToJson());
        });

        modelBuilder.Entity<Support>(entity => {
            entity.OwnsMany(s => s.Modifiers, m => m.ToJson());
        });

        modelBuilder.Entity<SkillPrerequisite>(entity => {
            entity.HasKey(sp => new { sp.SkillId, sp.PrerequisiteSkillId });
        });

        modelBuilder.Entity<NpcChatMessage>(entity => {
            entity.HasOne(m => m.Conversation).WithMany().HasForeignKey(m => m.ConversationId);
        });

        modelBuilder.Entity<WorldEvent>(entity => {
            entity.Property(e => e.Tags).HasColumnType("text[]");
            entity.OwnsOne(e => e.Region, r => {
                r.ToJson();
                r.OwnsOne(c => c.Center, center => center.OwnsOne(l => l.Coordinates));
            });
        });

        modelBuilder.Entity<World>(entity => {
            entity.OwnsOne(w => w.Boundary, b => b.ToJson());
        });

        modelBuilder.Entity<Country>(entity => {
            entity.OwnsOne(c => c.Boundary, b => {
                b.ToJson();
                b.OwnsMany(p => p.Points);
            });
        });

        modelBuilder.Entity<City>(entity => {
            entity.OwnsOne(c => c.Boundary, b => {
                b.ToJson();
                b.OwnsMany(p => p.Points);
            });
        });

        modelBuilder.Entity<Building>(entity => { entity.OwnsOne(b => b.Boundary, r => r.ToJson()); });

        modelBuilder.Entity<Prop>(entity => {
            entity.HasDiscriminator<string>("prop_type")
                  .HasValue<Chair>("Chair")
                  .HasValue<Table>("Table")
                  .HasValue<Bed>("Bed")
                  .HasValue<Chest>("Chest")
                  .HasValue<Fireplace>("Fireplace")
                  .HasValue<Bookcase>("Bookcase")
                  .HasValue<Barrel>("Barrel")
                  .HasValue<Forge>("Forge")
                  .HasValue<Altar>("Altar")
                  .HasValue<Counter>("Counter")
                  .HasValue<Lever>("Lever");
            entity.HasIndex(p => p.BuildingId);
            entity.OwnsOne(p => p.Boundary, b => b.ToJson());
            entity.Property(p => p.StorageItemCategories)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                    v => v == null ? null : JsonSerializer.Deserialize<List<ItemCategory>>(v, JsonOptions)
                )
                .HasColumnType("jsonb");
        });

        modelBuilder.Entity<PropItem>(entity => {
            entity.HasIndex(pi => new { pi.PropId, pi.Index }).IsUnique();
        });

        modelBuilder.Entity<Quest>(entity => {
            entity.Property(q => q.ItemRewards).HasColumnType("uuid[]");
            entity.Property(q => q.PrerequisiteQuestIds).HasColumnType("uuid[]");
            entity.HasIndex(q => q.WorldId);
            entity.HasIndex(q => q.GiverId);
        });

        modelBuilder.Entity<QuestObjective>(entity => {
            entity.OwnsOne(o => o.Region, r => {
                r.ToJson();
                r.OwnsOne(c => c.Center, center => center.OwnsOne(l => l.Coordinates));
            });
            entity.HasIndex(o => o.QuestId);
        });

        modelBuilder.Entity<PersonSkill>(entity => {
            entity.HasOne(ps => ps.Skill).WithMany().HasForeignKey(ps => ps.SkillId);
            entity.HasIndex(ps => new { ps.PersonId, ps.SkillId }).IsUnique();
        });

        modelBuilder.Entity<PersonQuest>(entity => {
            entity.HasOne(pq => pq.Quest).WithMany().HasForeignKey(pq => pq.QuestId);
            entity.HasIndex(pq => new { pq.PersonId, pq.QuestId }).IsUnique();
        });

        modelBuilder.Entity<PersonQuestObjective>(entity => {
            entity.HasOne(po => po.Objective).WithMany().HasForeignKey(po => po.ObjectiveId);
            entity.HasIndex(po => new { po.PersonId, po.ObjectiveId }).IsUnique();
        });

        modelBuilder.Entity<Job>(entity => {
            entity.OwnsOne(j => j.Location, lo => {
                lo.OwnsOne(l => l.Coordinates);
                lo.HasIndex(l => new { l.CityId, l.BuildingId });
            });
            entity.HasIndex(j => j.PersonId);
        });

        modelBuilder.Entity<World>()
            .HasIndex(w => w.Name).IsUnique();

        modelBuilder.Entity<Race>()
            .HasIndex(r => new { r.WorldId, r.Name }).IsUnique();

        modelBuilder.Entity<Profession>()
            .HasIndex(p => new { p.WorldId, p.Name }).IsUnique();

        modelBuilder.Entity<Faction>()
            .HasIndex(f => new { f.WorldId, f.Name }).IsUnique();

        modelBuilder.Entity<Country>()
            .HasIndex(c => new { c.WorldId, c.Name }).IsUnique();

        modelBuilder.Entity<City>()
            .HasIndex(c => new { c.CountryId, c.Name }).IsUnique();

        modelBuilder.Entity<Building>()
            .HasIndex(b => new { b.CityId, b.Name }).IsUnique();

        modelBuilder.Entity<Road>()
            .HasIndex(r => new { r.OriginCityId, r.DestinationCityId }).IsUnique();

        modelBuilder.Entity<NpcConversation>(entity => {
            entity.HasIndex(c => c.WorldId);
            entity.HasIndex(c => new { c.NpcId, c.PersonId }).IsUnique();
        });

        modelBuilder.Entity<NpcChatMessage>()
            .HasIndex(m => new { m.ConversationId, m.Index }).IsUnique();

        modelBuilder.Entity<WorldEvent>()
            .HasIndex(e => e.WorldId);

        modelBuilder.Entity<Reputation>()
            .HasIndex(r => new { r.PersonId, r.FactionId }).IsUnique();

        modelBuilder.Entity<FactionMember>(entity => {
            entity.HasIndex(fm => new { fm.PersonId, fm.FactionId }).IsUnique();
            entity.HasIndex(fm => fm.FactionId);
        });

        modelBuilder.Entity<BuildingOwner>(entity => {
            entity.HasIndex(bo => new { bo.BuildingId, bo.OwnerId }).IsUnique();
            entity.HasIndex(bo => bo.OwnerId);
        });
    }
}
