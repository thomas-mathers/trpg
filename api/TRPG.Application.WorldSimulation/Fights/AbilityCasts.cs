using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Fights;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.WorldSimulation.Poses;
using TRPG.Application.WorldSimulation.Publishing;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Fights;

internal sealed class AbilityCasts(
    Guid worldId,
    IServiceScopeFactory serviceScopeFactory,
    TransientCreatureWalkRegistry transientWalks
)
{
    private readonly Dictionary<Guid, ActiveFight> _fights = [];

    public async Task Cast(
        CastAbilityMessage message,
        LivePlayerPoses poses,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await Run(message, poses, now, cancellationToken);
            Track(message.PlayerId, result.Fight);
            message.Completion.TrySetResult(result);
        }
        catch (Exception exception)
        {
            message.Completion.TrySetException(exception);
            throw;
        }
    }

    public void End(Guid playerId) => _fights.Remove(playerId);

    private async Task<AbilityCastResult> Run(
        CastAbilityMessage message,
        LivePlayerPoses poses,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var pose = poses.Find(message.PlayerId);
        await using var scope = serviceScopeFactory.CreateAsyncScope();

        var result = await scope
            .ServiceProvider.GetRequiredService<
                ICommandHandler<CastPlayerAbilityCommand, AbilityCastResult>
            >()
            .Handle(
                new CastPlayerAbilityCommand
                {
                    WorldId = worldId,
                    PlayerId = message.PlayerId,
                    TargetId = message.TargetId,
                    AbilityName = message.AbilityName,
                    GameTime = now,
                    Fight = _fights.GetValueOrDefault(message.PlayerId),
                    PlayerPlacement = pose is null
                        ? null
                        : new PlayerPlacement(pose.Position, pose.Input.Heading),
                    FindLiveCreaturePosition = creatureId =>
                        pose is null
                            ? null
                            : transientWalks.Find(creatureId, pose.LocationId, now)?.Position,
                },
                cancellationToken
            );
        if (result.Succeeded)
        {
            await scope
                .ServiceProvider.GetRequiredService<IActionSceneAnnouncer>()
                .AnnounceActionChanges(worldId, message.PlayerId, now, cancellationToken);
        }

        return result;
    }

    private void Track(Guid playerId, ActiveFight? fight)
    {
        if (fight is null)
        {
            _fights.Remove(playerId);
            return;
        }

        _fights[playerId] = fight;
    }
}
