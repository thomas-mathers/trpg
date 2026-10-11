namespace TRPG.Application.WorldSimulation.Subscriptions;

public interface IPlayerConnectionCloser
{
    void Close(Guid playerId);
}
