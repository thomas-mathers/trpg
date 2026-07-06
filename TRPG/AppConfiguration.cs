namespace TRPG;

internal class AppConfiguration {
    public string OllamaModel { get; init; } = "llama3.1:8b";
    public bool OllamaThink { get; init; }
    public float? OllamaTemperature { get; init; }
    public Uri OllamaUri { get; init; } = new("http://127.0.0.1:11434");
    public string LogDirectory { get; init; } = "logs";

    public string PostgresConnectionString { get; init; } =
        "Host=127.0.0.1;Port=5432;Database=trpg;Username=postgres;Password=postgres;" +
        "Minimum Pool Size=1;Connection Idle Lifetime=86400";
}
