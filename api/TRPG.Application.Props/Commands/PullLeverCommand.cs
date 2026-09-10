using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class PullLeverCommand
{
    public required Guid LeverId { get; init; }
}

public record PullLeverResult(bool AlreadyPulled);

internal class PullLeverCommandHandler(IPropsDbContext context)
    : ICommandHandler<PullLeverCommand, PullLeverResult>
{
    public async Task<PullLeverResult> Handle(
        PullLeverCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var lever =
            await context
                .Props.OfType<Lever>()
                .FirstOrDefaultAsync(l => l.Id == command.LeverId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Lever), command.LeverId);

        if (lever.IsPulled)
        {
            return new PullLeverResult(AlreadyPulled: true);
        }

        lever.IsPulled = true;
        await context.SaveChangesAsync(cancellationToken);

        return new PullLeverResult(AlreadyPulled: false);
    }
}
