using TRPG.Application.Common.Events;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.Application.GameTurns.Events;

internal record SceneUpdatedEvent(SceneSnapshot Scene) : GameClientEvent
{
    public override string MethodName => "SceneSnapshot";
    public override object? Payload => Scene;
}
