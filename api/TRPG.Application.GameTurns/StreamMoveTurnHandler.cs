namespace TRPG.Application.GameTurns;

internal class StreamMoveTurnHandler(GameTurnStreamer streamer, MoveTurnResolver resolver)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        Guid connectorId,
        CancellationToken cancellationToken = default
    ) =>
        streamer.StreamTurn(
            session,
            ct => resolver.Resolve(session, connectorId, ct),
            cancellationToken
        );
}
