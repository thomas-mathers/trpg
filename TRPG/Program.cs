using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Data;
using TRPG.Services;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .Build();
    
var services = new ServiceCollection()
    .AddSingleton<IConfiguration>(config)
    .AddDbContext<TrpgDbContext>(options =>
    {
        var connectionString = config.GetConnectionString("Default");
        options.UseNpgsql(connectionString);
    })
    .AddMemoryCache()
    .AddTransient<BuildingService>()
    .AddTransient<FactionService>()
    .AddTransient<InventoryService>()
    .AddTransient<ItemService>()
    .AddTransient<JobService>()
    .AddTransient<LocationService>()
    .AddTransient<NavigationService>()
    .AddTransient<NpcConversationService>()
    .AddTransient<PersonService>()
    .AddTransient<ProfessionService>()
    .AddTransient<QuestService>()
    .AddTransient<RaceService>()
    .AddTransient<ReputationService>()
    .AddTransient<SkillService>()
    .AddTransient<WorldEventService>()
    .BuildServiceProvider();
    
