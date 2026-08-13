namespace TRPG.Application.GameSessions;

public abstract record GameClientEvent
{
    public abstract string MethodName { get; }
    public virtual object? Payload => null;
}
