namespace TRPG.Application.GameSessions;

public abstract record GameTurnEvent
{
    public abstract string MethodName { get; }
    public virtual object? Payload => null;
}
