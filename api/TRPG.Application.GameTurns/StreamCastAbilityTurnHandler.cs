namespace TRPG.Application.GameTurns;

internal class StreamCastAbilityTurnHandler(
    GameTurnStreamer streamer,
    CastAbilityTurnResolver resolver
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        Guid targetId,
        string abilityName,
        CancellationToken cancellationToken = default
    ) =>
        streamer.StreamTurn(
            session,
            ct => resolver.Resolve(session, targetId, abilityName, ct),
            cancellationToken
        );
}
