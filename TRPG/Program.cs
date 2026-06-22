using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using TRPG;
using TRPG.Data;
using TRPG.Services;

var appConfiguration = new AppConfiguration();

var services = new ServiceCollection()
    .AddDbContext<TrpgDbContext>(options => {
        var connectionString = appConfiguration.PostgresConnectionString;
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
    .AddSingleton<OllamaApiClient>(_ => {
        var client = new OllamaApiClient(appConfiguration.OllamaUri) {
            SelectedModel = appConfiguration.OllamaModel
        };
        return client;
    })
    .BuildServiceProvider();