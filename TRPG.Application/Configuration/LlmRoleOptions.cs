namespace TRPG.Application.Configuration;

public enum LlmProvider
{
    Ollama,
    Anthropic,
}

public class LlmRoleOptions
{
    public LlmProvider Provider { get; init; } = LlmProvider.Ollama;
    public string Model { get; init; } = "llama3.1:8b";
    public bool? Think { get; init; }
    public float? Temperature { get; init; }
}
