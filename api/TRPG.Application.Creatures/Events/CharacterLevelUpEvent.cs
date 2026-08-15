using TRPG.Application.Common.Events;

namespace TRPG.Application.Creatures.Events;

internal record CharacterLevelUpEvent(int Level) : GameClientEvent
{
    public override string MethodName => "CharacterLevelUp";
    public override object? Payload => this;
}
