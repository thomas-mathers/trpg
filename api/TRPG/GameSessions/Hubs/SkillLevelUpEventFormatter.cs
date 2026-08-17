using TRPG.Abilities.Mappers;
using TRPG.Application.Creatures.Events;
using TRPG.Creatures.ClientModels;

namespace TRPG.GameSessions.Hubs;

internal sealed class SkillLevelUpEventFormatter : GameClientEventFormatter<SkillLevelUpEvent>
{
    protected override Task Dispatch(IGameClient client, SkillLevelUpEvent gameEvent) =>
        client.SkillLevelUp(
            new SkillLevelUp(
                gameEvent.Skill.ToResponse(),
                gameEvent.Level,
                gameEvent.CharacterExperienceCurrent,
                gameEvent.CharacterExperienceToNextLevel
            )
        );
}
