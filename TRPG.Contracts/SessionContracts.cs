namespace TRPG.Contracts;

public record CreateSessionResponse(Guid SessionId, string Response, TurnMetricsDto? Metrics);

public record ChatRequest(string Message);

public record ChatResponse(string Response, TurnMetricsDto? Metrics);

public record WaitRequest(int Hours);

public record WaitResponse(string Message, SceneResult Scene);

public record TurnMetricsDto(long FirstTokenMs, long TotalMs, int TokenCount, double TokensPerSecond);
