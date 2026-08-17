using TRPG.Abilities.Mappers;
using TRPG.Application.Creatures.Events;
using TRPG.Creatures.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class SkillLevelUpEventMapper : GameClientEventMapper<SkillLevelUpEvent>
{
    protected override IGameClientCall Map(SkillLevelUpEvent gameEvent) =>
        new GameClientCall<SkillLevelUp>(
            new SkillLevelUp(
                gameEvent.Skill.ToResponse(),
                gameEvent.Level,
                gameEvent.CharacterExperienceCurrent,
                gameEvent.CharacterExperienceToNextLevel
            ),
            static (client, arguments) => client.SkillLevelUp(arguments)
        );
}
