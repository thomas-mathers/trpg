namespace TRPG;

internal class OllamaOptions
{
    public Uri Uri { get; init; } = new("http://127.0.0.1:11434");
}

internal class LoggingOptions
{
    public string LogDirectory { get; init; } = "logs";
}
