namespace TRPG;

internal enum LlmProvider
{
    Ollama,
    Anthropic,
}

internal class LlmRoleSettings
{
    public LlmProvider Provider { get; init; } = LlmProvider.Ollama;
    public string Model { get; init; } = "llama3.1:8b";
    public bool Think { get; init; }
    public float? Temperature { get; init; }
}

internal class AppConfiguration
{
    public Uri OllamaUri { get; init; } = new("http://127.0.0.1:11434");
    public string LogDirectory { get; init; } = "logs";
    public LlmRoleSettings WorldGeneration { get; init; } = new();
    public LlmRoleSettings Gameplay { get; init; } = new();

    public string PostgresConnectionString { get; init; } =
        "Host=127.0.0.1;Port=5432;Database=trpg;Username=postgres;Password=postgres;"
        + "Minimum Pool Size=1;Connection Idle Lifetime=86400";
}
