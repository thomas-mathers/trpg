using System.ComponentModel;
using TRPG.Application.Common.Commands;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.GameTurns.Queries;
using TRPG.Tools;

namespace TRPG.NpcConversations.Tools;

internal class ShareExpeditionDiscoveryTool(
    GameTurnContext turnContext,
    ICommandHandler<ShareExpeditionDiscoveryCommand, DungeonConversationKnowledge?> shareDiscovery
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("share_expedition_discovery")]
    [Description(
        "Call only when the player explicitly tells the survivor the journal's news. Copy ExpeditionId from PlayerCanShare in the conversation briefing. Never call merely because conversation starts or the player owns the journal. Only success establishes that the survivor learned the news; narrate using the returned knowledge."
    )]
    private async Task<object?> InvokeAsync(Guid expeditionId, CancellationToken cancellationToken)
    {
        try
        {
            return await shareDiscovery.Handle(
                new ShareExpeditionDiscoveryCommand(
                    WorldId: turnContext.WorldId,
                    SessionId: turnContext.SessionId,
                    PlayerId: turnContext.PlayerId,
                    ExpeditionId: expeditionId
                ),
                cancellationToken
            );
        }
        catch (InvalidOperationException exception)
        {
            return new ToolError(exception.Message);
        }
    }
}
