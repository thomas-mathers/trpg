namespace TRPG.GameSessions.Responses;

public record ChatResponse(string Response, TurnMetricsDto? Metrics);

public record TurnMetricsDto(
    long FirstTokenMs,
    long TotalMs,
    int TokenCount,
    double TokensPerSecond
);
