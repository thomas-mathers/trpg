using Microsoft.EntityFrameworkCore;
using TRPG.Application.Books.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Knowledge.Commands;
using TRPG.Application.Quests.Results;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests;

internal sealed class FactDisclosureAttemptRecorder(
    IQuestsDbContext context,
    ICommandHandler<LearnFactCommand, bool> learnFact,
    IQueryHandler<GetFactByIdQuery, Fact?> getFact
)
{
    internal async Task<DisclosedFact?> Record(
        FactDisclosureRequest request,
        Guid? reasonFactId,
        CancellationToken cancellationToken
    )
    {
        var attempted = await context.FactDisclosureAttempts.AnyAsync(
            attempt =>
                attempt.WorldId == request.WorldId
                && attempt.PlayerId == request.PlayerId
                && attempt.NpcId == request.NpcId
                && attempt.FactId == request.FactId,
            cancellationToken
        );

        if (!attempted)
        {
            context.FactDisclosureAttempts.Add(
                new FactDisclosureAttempt
                {
                    WorldId = request.WorldId,
                    PlayerId = request.PlayerId,
                    NpcId = request.NpcId,
                    FactId = request.FactId,
                }
            );
            await context.SaveChangesAsync(cancellationToken);
            return null;
        }

        return reasonFactId is { } reasonId && reasonId != request.FactId
            ? await DiscloseReason(request, reasonId, cancellationToken)
            : null;
    }

    private async Task<DisclosedFact?> DiscloseReason(
        FactDisclosureRequest request,
        Guid factId,
        CancellationToken cancellationToken
    )
    {
        var fact =
            await getFact.Handle(new GetFactByIdQuery { FactId = factId }, cancellationToken)
            ?? throw new EntityNotFoundException("Fact", factId);
        var learned = await learnFact.Handle(
            new LearnFactCommand
            {
                WorldId = request.WorldId,
                KnowerId = request.PlayerId,
                FactId = factId,
            },
            cancellationToken
        );
        return learned ? new DisclosedFact(factId, fact.Value) : null;
    }
}
