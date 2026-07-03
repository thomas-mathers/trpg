namespace TRPG;

internal static class WorldGenerationDefaults {
    public const int CityTileSize = 100;
    public const int MaxBuildingsPerState = 2;
    public const int MaxFactionMembers = 6;
    public const int MinBuildingsPerState = 0;
    public const int MinFactionMembers = 3;
    public const int FactionCount = 8;
    public const int HousesPerCity = 12;
    public const int MinHouseholdSize = 1;
    public const int MaxHouseholdSize = 3;
    public const int DistrictsPerCity = 5;
    public const int MaxRuralStates = 10;
    public const int MaxCityStates = 40;
    public const int MaxCountries = 5;
    public const int MinRuralStates = 5;
    public const int MinCityStates = 20;
    public const int MinCountries = 3;
    public const int RaceCount = 6;
    public const int WorldHeight = 10000;
    public const int WorldWidth = 10000;
    public const string Description = "Medieval";
}

internal class AppConfiguration {
    public string OllamaModel { get; init; } = "gemma4:latest";
    public Uri OllamaUri { get; init; } = new("http://localhost:11434");

    public string PostgresConnectionString { get; init; } =
        "Host=localhost;Port=5432;Database=trpg;Username=postgres;Password=postgres";
}