namespace TRPG;

internal static class WorldGenerationDefaults {
    public const int CityTileSize = 100;
    public const int FactionCount = 8;
    public const int HousesPerCity = 12;
    public const int MaxCities = 40;
    public const int MaxCountries = 5;
    public const int MinCities = 20;
    public const int MinCountries = 3;
    public const int RaceCount = 6;
    public const int WorldHeight = 10000;
    public const int WorldWidth = 10000;
}

internal class AppConfiguration {
    public string OllamaModel { get; init; } = "qwen2.5:14b";
    public Uri OllamaUri { get; init; } = new("http://localhost:11434");

    public string PostgresConnectionString { get; init; } =
        "Host=localhost;Port=5432;Database=trpg;Username=postgres;Password=postgres";
}