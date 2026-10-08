using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Routing.Commands;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class CreaturesReleasedEventHandler(
    ICommandHandler<
        ResumeReleasedRouteTravelersCommand,
        IReadOnlyCollection<Guid>
    > resumeReleasedRouteTravelers
) : IDomainEventConsumer<CreaturesReleasedEvent>
{
    public async Task Handle(
        CreaturesReleasedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        await resumeReleasedRouteTravelers.Handle(
            new ResumeReleasedRouteTravelersCommand
            {
                ReleasedCreatureIds = domainEvent.CreatureIds,
                GameTime = domainEvent.GameTime,
            },
            cancellationToken
        );
    }
}
