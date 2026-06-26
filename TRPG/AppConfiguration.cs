namespace TRPG;

internal static class WorldGenerationDefaults {
    public const int AttackCount = 30;
    public const int BuildingsPerCity = 8;
    public const int CountryCount = 3;
    public const int FactionCount = 8;
    public const int MaxCities = 8;
    public const int MinCities = 5;
    public const int ProfessionCount = 8;
    public const int RaceCount = 6;
    public const int SupportCount = 15;
}

internal class AppConfiguration {
    public string OllamaModel { get; init; } = "qwen2.5:14b";
    public Uri OllamaUri { get; init; } = new("http://localhost:11434");

    public string PostgresConnectionString { get; init; } =
        "Host=localhost;Port=5432;Database=trpg;Username=postgres;Password=postgres";
}