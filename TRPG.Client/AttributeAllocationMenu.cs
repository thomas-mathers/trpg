using Spectre.Console;
using TRPG.Client.Extensions;

namespace TRPG.Client;

internal sealed class AttributeAllocationMenu(TrpgHttpClient client, Guid playerId)
{
    public async Task Run(CancellationToken cancellationToken)
    {
        var totalPoints = await client.GetUnallocatedAttributePoints(playerId, cancellationToken);
        if (totalPoints <= 0)
        {
            AnsiConsole.AnnounceWarning("No attribute points available to allocate.");
            return;
        }

        var baseAttributes = await client.GetBaseAttributes(playerId, cancellationToken);

        var deltas = await AttributeAllocationPrompt.Run(
            totalPoints,
            baseAttributes,
            cancellationToken
        );
        if (deltas == null)
        {
            AnsiConsole.AnnounceWarning("Allocation cancelled.");
            return;
        }

        if (deltas.Count == 0)
        {
            AnsiConsole.AnnounceWarning("No points allocated.");
            return;
        }

        await client.AllocateAttributePoints(playerId, deltas, cancellationToken);
        AnsiConsole.AnnounceSuccess("Attributes updated!");
    }
}
