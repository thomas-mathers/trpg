namespace TRPG;

internal class AppConfiguration {
    public string OllamaModel { get; init; } = "qwen2.5:14b";
    public Uri OllamaUri { get; init; } = new("http://localhost:11434");

    public string PostgresConnectionString { get; init; } =
        "Host=localhost;Port=5432;Database=trpg;Username=postgres;Password=postgres";
}