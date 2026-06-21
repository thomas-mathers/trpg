using Microsoft.EntityFrameworkCore;
using TRPG.Models;

namespace TRPG.Data;

internal class TrpgDbContext(DbContextOptions<TrpgDbContext> options) : DbContext(options)
{
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<SkillPrerequisite> SkillPrerequisites => Set<SkillPrerequisite>();
    public DbSet<Effect> Effects => Set<Effect>();
    public DbSet<NpcConversation> NpcConversations => Set<NpcConversation>();
    public DbSet<NpcChatMessage> NpcChatMessages => Set<NpcChatMessage>();
    public DbSet<WorldEvent> WorldEvents => Set<WorldEvent>();
    public DbSet<Faction> Factions => Set<Faction>();
    public DbSet<FactionMember> FactionMembers => Set<FactionMember>();
    public DbSet<Reputation> Reputations => Set<Reputation>();
    public DbSet<World> Worlds => Set<World>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Race> Races => Set<Race>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<BuildingProp> BuildingProps => Set<BuildingProp>();
    public DbSet<BuildingOwner> BuildingOwners => Set<BuildingOwner>();
    public DbSet<Quest> Quests => Set<Quest>();
    public DbSet<QuestObjective> QuestObjectives => Set<QuestObjective>();
    public DbSet<PersonQuest> PersonQuests => Set<PersonQuest>();
    public DbSet<PersonQuestObjective> PersonQuestObjectives => Set<PersonQuestObjective>();
    public DbSet<Profession> Professions => Set<Profession>();
    public DbSet<PersonSkill> PersonSkills => Set<PersonSkill>();
    public DbSet<TravelRoute> TravelRoutes => Set<TravelRoute>();
    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSnakeCaseNamingConvention();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<EquipmentSlot>().HaveConversion<string>();
        configurationBuilder.Properties<ItemCategory>().HaveConversion<string>();
        configurationBuilder.Properties<EffectStat>().HaveConversion<string>();
        configurationBuilder.Properties<EffectApplicationMode>().HaveConversion<string>();
        configurationBuilder.Properties<EffectType>().HaveConversion<string>();
        configurationBuilder.Properties<FactionRole>().HaveConversion<string>();
        configurationBuilder.Properties<QuestStatus>().HaveConversion<string>();
        configurationBuilder.Properties<QuestObjectiveType>().HaveConversion<string>();
        configurationBuilder.Properties<JobAction>().HaveConversion<string>();
        configurationBuilder.Properties<QuestTargetType>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(entity =>
        {
            entity.OwnsOne(p => p.Location, lo =>
            {
                lo.OwnsOne(l => l.Coordinates);
                lo.HasIndex(l => new { l.WorldId, l.CityId, l.BuildingId });
            });
            entity.OwnsOne(p => p.Progression, prog =>
            {
                prog.ToJson();
                prog.OwnsOne(p => p.Experience);
            });
            entity.OwnsOne(p => p.Attributes, s =>
            {
                s.ToJson();
                s.OwnsOne(st => st.Hp);
                s.OwnsOne(st => st.Ap);
            });
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.Property(i => i.ActiveEffectIds).HasColumnType("uuid[]");
            entity.Property(i => i.PassiveEffectIds).HasColumnType("uuid[]");
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasOne(i => i.Item).WithMany().HasForeignKey(i => i.ItemId);
            entity.HasIndex(i => i.PersonId);
        });

        modelBuilder.Entity<Skill>(entity =>
        {
            entity.Property(s => s.ActiveEffectIds).HasColumnType("uuid[]");
            entity.Property(s => s.PassiveEffectIds).HasColumnType("uuid[]");
        });

        modelBuilder.Entity<SkillPrerequisite>(entity =>
        {
            entity.HasKey(sp => new { sp.SkillId, sp.PrerequisiteSkillId });
        });

        modelBuilder.Entity<NpcChatMessage>(entity =>
        {
            entity.HasOne(m => m.Conversation).WithMany().HasForeignKey(m => m.ConversationId);
        });

        modelBuilder.Entity<WorldEvent>(entity =>
        {
            entity.Property(e => e.Tags).HasColumnType("text[]");
            entity.OwnsOne(e => e.Region, r =>
            {
                r.ToJson();
                r.OwnsOne(c => c.Center, center => center.OwnsOne(l => l.Coordinates));
            });
        });

        modelBuilder.Entity<Country>(entity =>
        {
            entity.OwnsOne(c => c.Boundary, b =>
            {
                b.ToJson();
                b.OwnsOne(c => c.Center, center => center.OwnsOne(l => l.Coordinates));
            });
        });

        modelBuilder.Entity<Province>(entity =>
        {
            entity.OwnsOne(p => p.Boundary, b =>
            {
                b.ToJson();
                b.OwnsOne(c => c.Center, center => center.OwnsOne(l => l.Coordinates));
            });
        });

        modelBuilder.Entity<City>(entity =>
        {
            entity.OwnsOne(c => c.Boundary, b =>
            {
                b.ToJson();
                b.OwnsOne(c => c.Center, center => center.OwnsOne(l => l.Coordinates));
            });
        });

        modelBuilder.Entity<Building>(entity =>
        {
            entity.OwnsOne(b => b.Boundary, r => r.ToJson());
        });

        modelBuilder.Entity<BuildingProp>(entity =>
        {
            entity.OwnsOne(bp => bp.Coordinates, p => p.ToJson());
        });

        modelBuilder.Entity<Quest>(entity =>
        {
            entity.Property(q => q.ItemRewards).HasColumnType("uuid[]");
            entity.Property(q => q.PrerequisiteQuestIds).HasColumnType("uuid[]");
        });

        modelBuilder.Entity<QuestObjective>(entity =>
        {
            entity.OwnsOne(o => o.Region, r =>
            {
                r.ToJson();
                r.OwnsOne(c => c.Center, center => center.OwnsOne(l => l.Coordinates));
            });
        });

        modelBuilder.Entity<PersonSkill>(entity =>
        {
            entity.HasOne(ps => ps.Skill).WithMany().HasForeignKey(ps => ps.SkillId);
            entity.HasIndex(ps => new { ps.PersonId, ps.SkillId }).IsUnique();
        });

        modelBuilder.Entity<PersonQuest>(entity =>
        {
            entity.HasOne(pq => pq.Quest).WithMany().HasForeignKey(pq => pq.QuestId);
            entity.HasIndex(pq => new { pq.PersonId, pq.QuestId }).IsUnique();
        });

        modelBuilder.Entity<PersonQuestObjective>(entity =>
        {
            entity.HasOne(po => po.Objective).WithMany().HasForeignKey(po => po.ObjectiveId);
            entity.HasIndex(po => new { po.PersonId, po.ObjectiveId }).IsUnique();
        });

        modelBuilder.Entity<Job>(entity =>
        {
            entity.OwnsOne(j => j.Location, lo =>
            {
                lo.OwnsOne(l => l.Coordinates);
                lo.HasIndex(l => new { l.WorldId, l.CityId, l.BuildingId });
            });
            entity.HasIndex(j => j.PersonId);
        });

        modelBuilder.Entity<World>()
            .HasIndex(w => w.Name).IsUnique();

        modelBuilder.Entity<Race>()
            .HasIndex(r => r.Name).IsUnique();

        modelBuilder.Entity<Profession>()
            .HasIndex(p => p.Name).IsUnique();

        modelBuilder.Entity<Skill>()
            .HasIndex(s => s.Name).IsUnique();

        modelBuilder.Entity<Effect>()
            .HasIndex(e => e.Name).IsUnique();

        modelBuilder.Entity<Item>()
            .HasIndex(i => i.Name).IsUnique();

        modelBuilder.Entity<Faction>()
            .HasIndex(f => f.Name).IsUnique();

        modelBuilder.Entity<Country>()
            .HasIndex(c => new { c.WorldId, c.Name }).IsUnique();

        modelBuilder.Entity<Province>()
            .HasIndex(p => new { p.CountryId, p.Name }).IsUnique();

        modelBuilder.Entity<City>()
            .HasIndex(c => new { c.ProvinceId, c.Name }).IsUnique();

        modelBuilder.Entity<Building>()
            .HasIndex(b => new { b.CityId, b.Name }).IsUnique();

        modelBuilder.Entity<TravelRoute>()
            .HasIndex(r => new { r.OriginCityId, r.DestinationCityId }).IsUnique();

        modelBuilder.Entity<NpcConversation>()
            .HasIndex(c => new { c.NpcId, c.PersonId }).IsUnique();

        modelBuilder.Entity<NpcChatMessage>()
            .HasIndex(m => new { m.ConversationId, m.Index }).IsUnique();

        modelBuilder.Entity<WorldEvent>()
            .HasIndex(e => e.WorldId);

        modelBuilder.Entity<Reputation>()
            .HasIndex(r => new { r.PersonId, r.FactionId }).IsUnique();

        modelBuilder.Entity<FactionMember>(entity =>
        {
            entity.HasIndex(fm => new { fm.PersonId, fm.FactionId }).IsUnique();
            entity.HasIndex(fm => fm.FactionId);
        });

        modelBuilder.Entity<BuildingOwner>(entity =>
        {
            entity.HasIndex(bo => new { bo.BuildingId, bo.OwnerId }).IsUnique();
            entity.HasIndex(bo => bo.OwnerId);
        });

        modelBuilder.Entity<Quest>()
            .HasIndex(q => q.GiverId);

        modelBuilder.Entity<QuestObjective>()
            .HasIndex(o => o.QuestId);
    }
}
