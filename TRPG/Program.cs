using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using TRPG;
using TRPG.Commands;
using TRPG.Data;
using TRPG.Services;
using ZLogger;
using ZLogger.Providers;

Directory.CreateDirectory("logs");

foreach (var old in Directory.GetFiles("logs", "trpg_*.log")
             .Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddDays(-7))) {
    File.Delete(old);
}

var appConfiguration = new AppConfiguration();

var services = new ServiceCollection()
    .AddLogging(builder => {
        builder.SetMinimumLevel(LogLevel.Trace);
        builder.AddFilter("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Error);
        builder.AddZLoggerRollingFile(options => {
            options.FilePathSelector = (timestamp, sequence) =>
                Path.Combine("logs", $"trpg_{timestamp.LocalDateTime:yyyyMMdd}_{sequence:000}.log");
            options.RollingInterval = RollingInterval.Day;
            options.UsePlainTextFormatter(formatter => {
                formatter.SetPrefixFormatter($"{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}: ",
                    (in template, in info) =>
                        template.Format(info.Timestamp.Local, info.LogLevel, info.Category));
            });
        });
    })
    .AddDbContext<TrpgDbContext>((provider, options) => {
        options.UseNpgsql(appConfiguration.PostgresConnectionString)
            .UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());
    }, ServiceLifetime.Transient)
    .AddMemoryCache()
    .AddTransient<BuildingService>()
    .AddTransient<FactionService>()
    .AddTransient<InventoryService>()
    .AddTransient<ItemService>()
    .AddTransient<JobService>()
    .AddTransient<LocationService>()
    .AddTransient<NavigationService>()
    .AddTransient<NpcConversationService>()
    .AddTransient<AbilityService>()
    .AddTransient<SkillService>()
    .AddTransient<PersonService>()
    .AddTransient<QuestService>()
    .AddTransient<RaceService>()
    .AddTransient<ReputationService>()
    .AddTransient<WorldEventService>()
    .AddSingleton<OllamaApiClient>(_ => {
        var httpClient = new HttpClient
            { BaseAddress = appConfiguration.OllamaUri, Timeout = Timeout.InfiniteTimeSpan };
        return new OllamaApiClient(httpClient) { SelectedModel = appConfiguration.OllamaModel };
    })
    .AddTransient<GenerateGeographyCommandHandler>()
    .AddTransient<GenerateRacesCommandHandler>()
    .AddTransient<GenerateFactionsCommandHandler>()
    .AddTransient<GenerateWorldCommandHandler>()
    .AddTransient<BootstrapWorldCommandHandler>()
    .AddTransient<DropWorldCommandHandler>()
    .AddTransient<Menu>()
    .BuildServiceProvider();

if (args.Length > 0) {
    await RunCommand(args, services);
    return;
}

await services.GetRequiredService<Menu>().Run();

static async Task RunCommand(string[] args, IServiceProvider services) {
    var description = args.Length > 1
        ? string.Join(" ", args.Skip(1))
        : "A dark medieval world of knights, political intrigue, and ancient forbidden magic";

    var sw = Stopwatch.StartNew();

    switch (args[0]) {
        case "generate-geography": {
            var handler = services.GetRequiredService<GenerateGeographyCommandHandler>();
            var result = await handler.Handle(new GenerateGeographyCommand { Description = description },
                CancellationToken.None);
            Console.WriteLine($"\nWorld: {result.World.Name}");
            Console.WriteLine(
                $"Countries ({result.Countries.Count}), Cities ({result.Cities.Count}), Roads ({result.Roads.Count})");
            foreach (var country in result.Countries) {
                var citiesInCountry = result.Cities.Where(c => c.CountryId == country.Id).ToList();
                Console.WriteLine($"\n  {country.Name}:");
                Console.WriteLine(
                    $"    Cities: {string.Join(", ", citiesInCountry.Select(c => c.IsCapital ? $"[{c.Name}]" : c.Name))}");
            }

            Console.WriteLine($"\nRoads: {string.Join(", ", result.Roads.Take(8).Select(r => r.Name))}");
            break;
        }
        case "generate-races": {
            var handler = services.GetRequiredService<GenerateRacesCommandHandler>();
            var races = await handler.Handle(
                new GenerateRacesCommand
                    { Count = WorldGenerationDefaults.RaceCount, Description = description, WorldId = Guid.NewGuid() },
                CancellationToken.None);
            Console.WriteLine($"\nRaces ({races.Count}):");
            foreach (var r in races) {
                Console.WriteLine($"  {r.Name} [{r.CultureStyle}]: {r.Description}");
            }

            break;
        }
        case "generate-factions": {
            var handler = services.GetRequiredService<GenerateFactionsCommandHandler>();
            var factions =
                await handler.Handle(
                    new GenerateFactionsCommand {
                        Count = WorldGenerationDefaults.FactionCount, Description = description,
                        WorldId = Guid.NewGuid()
                    }, CancellationToken.None);
            Console.WriteLine($"\nFactions ({factions.Count}):");
            foreach (var f in factions) {
                Console.WriteLine($"  {f.Name}: {f.Description}");
            }

            break;
        }
        case "generate-abilities": {
            Console.WriteLine($"\nAttacks ({AbilityDefinitions.Attacks.Count}):");
            foreach (var a in AbilityDefinitions.Attacks.Take(5)) {
                Console.WriteLine($"  [{a.Skill} lv{a.RequiredSkillLevel}] {a.Name}: {a.Description}");
            }

            if (AbilityDefinitions.Attacks.Count > 5) {
                Console.WriteLine($"  ... and {AbilityDefinitions.Attacks.Count - 5} more");
            }

            Console.WriteLine($"\nSupports ({AbilityDefinitions.Supports.Count}):");
            foreach (var s in AbilityDefinitions.Supports.Take(5)) {
                Console.WriteLine($"  [{s.Skill} lv{s.RequiredSkillLevel}] {s.Name}: {s.Description}");
            }

            if (AbilityDefinitions.Supports.Count > 5) {
                Console.WriteLine($"  ... and {AbilityDefinitions.Supports.Count - 5} more");
            }

            break;
        }
        case "generate-names": {
            Console.WriteLine("\nName pools by culture:");
            foreach (var culture in new[] {
                "Nordic/Viking", "Roman", "Feudal Japanese", "Arabic/Persian", "Celtic", "Slavic", "Ancient Egyptian",
                "Mesoamerican", "Byzantine", "Mongol"
            }) {
                var (firstNames, lastNames) = NameDefinitions.GetPool(culture);
                Console.WriteLine($"\n  {culture}:");
                Console.WriteLine($"    First ({firstNames.Length}): {string.Join(", ", firstNames)}");
                Console.WriteLine($"    Last  ({lastNames.Length}): {string.Join(", ", lastNames)}");
            }

            break;
        }
        case "generate-world": {
            var handler = services.GetRequiredService<GenerateWorldCommandHandler>();
            var result = await handler.Handle(new GenerateWorldCommand {
                Description = description,
                FactionCount = WorldGenerationDefaults.FactionCount,
                HousesPerCity = WorldGenerationDefaults.HousesPerCity,
                MaxCities = WorldGenerationDefaults.MaxCities,
                MaxCountries = WorldGenerationDefaults.MaxCountries,
                MinCities = WorldGenerationDefaults.MinCities,
                MinCountries = WorldGenerationDefaults.MinCountries,
                RaceCount = WorldGenerationDefaults.RaceCount
            }, CancellationToken.None);

            Console.WriteLine($"\nWorld: {result.World.Name}");
            Console.WriteLine($"{result.World.Description}");
            Console.WriteLine(
                $"\nRaces: {string.Join(", ", result.Races.Select(r => $"{r.Name} [{r.CultureStyle}]"))}");
            Console.WriteLine($"Factions: {string.Join(", ", result.Factions.Select(f => f.Name))}");
            Console.WriteLine(
                $"\nCountries ({result.Countries.Count}), Cities ({result.Cities.Count}), Roads ({result.Roads.Count}):");
            foreach (var country in result.Countries) {
                var citiesInCountry = result.Cities.Where(c => c.CountryId == country.Id).ToList();
                Console.WriteLine($"\n  {country.Name}: {country.Description}");
                Console.WriteLine(
                    $"    Cities: {string.Join(", ", citiesInCountry.Select(c => c.IsCapital ? $"[{c.Name}]" : c.Name))}");
            }

            Console.WriteLine(
                $"\nAbilities: {AbilityDefinitions.Attacks.Count} attacks, {AbilityDefinitions.Supports.Count} supports");
            Console.WriteLine($"Buildings: {result.Buildings.Count}, Persons: {result.Persons.Count}");
            break;
        }
        default:
            Console.WriteLine($"Unknown command: {args[0]}");
            Console.WriteLine(
                "Available commands: generate-world, generate-geography, generate-races, generate-professions, generate-factions, generate-skills, generate-names");
            break;
    }

    Console.WriteLine($"Total: {sw.Elapsed.TotalSeconds:F1}s");
}