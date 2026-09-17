using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.Quests.Results;

namespace TRPG.Application.Quests.Commands;

public class AskAboutFactCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid NpcId { get; init; }
    public required Guid FactId { get; init; }
}

// Reputation and completed supporting quests alone, no bribe or intimidation component. Free of
// stakes and deterministic, so there is no lockout to worry about — repeating this without
// anything else changing always gives the same answer.
internal class AskAboutFactCommandHandler(
    FactDisclosureResolver resolver,
    IOptionsMonitor<FactDisclosureOptions> factDisclosureOptions
) : ICommandHandler<AskAboutFactCommand, FactDisclosureResult>
{
    public Task<FactDisclosureResult> Handle(
        AskAboutFactCommand command,
        CancellationToken cancellationToken = default
    ) =>
        resolver.Resolve(
            command.WorldId,
            command.PlayerId,
            command.NpcId,
            command.FactId,
            approach: null,
            computeApproachContribution: _ => 0,
            factDisclosureOptions.CurrentValue,
            cancellationToken
        );
}
