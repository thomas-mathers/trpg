using TRPG.Application.Common.Commands;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures;

public class SneakDetectionService(
    SkillCheckService skillCheckService,
    ICommandHandler<AdjustCreatureSkillsCommand> adjustCreatureSkills,
    ICommandHandler<ToggleSneakingCommand, ToggleSneakingCommandResult> toggleSneaking
)
{
    public async Task<bool> RollDetection(
        Guid worldId,
        Guid creatureId,
        bool isSneaking,
        GameInstant gameTime,
        SkillCheckCurve curve,
        CancellationToken cancellationToken = default
    )
    {
        if (!isSneaking)
        {
            return true;
        }

        var isDetected = await skillCheckService.Roll(
            creatureId,
            Skill.Sneak,
            curve,
            cancellationToken
        );

        if (isDetected)
        {
            await toggleSneaking.Handle(
                new ToggleSneakingCommand { CreatureId = creatureId },
                cancellationToken
            );
        }
        else
        {
            await adjustCreatureSkills.Handle(
                new AdjustCreatureSkillsCommand
                {
                    WorldId = worldId,
                    CreatureId = creatureId,
                    GameTime = gameTime,
                    UsageCounts = new Dictionary<Skill, int> { [Skill.Sneak] = 1 },
                },
                cancellationToken
            );
        }

        return isDetected;
    }
}
