using System.ComponentModel;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Quests.Results;
using TRPG.Domain.Models;
using TRPG.Tools;

namespace TRPG.NpcConversations.Tools;

internal class OfferBribeForFactTool(
    GameTurnContext turnContext,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreatureByNameAtLocationQuery, Creature?> getCreatureByNameAtLocation,
    IQueryHandler<
        GetActiveLearnFactObjectiveForNpcQuery,
        LearnFactFromCreatureObjective?
    > getActiveObjectiveForNpc,
    ICommandHandler<OfferBribeForFactCommand, FactDisclosureResult> offerBribeForFact,
    ILogger<OfferBribeForFactTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("offer_bribe")]
    [Description(
        "Offers someone gold to explain what they're withholding — call this only when the player explicitly offers payment for the information, naming an amount. The gold is actually spent if (and only if) the offer succeeds; a failed or rejected offer costs nothing. Never decide the outcome yourself — narrate only what this returns. Outcome values: Disclosed (they took the offer, the gold is spent, and they told you — narrate FactText faithfully), CannotAfford (the player does not have that much gold to offer — narrate the offer falling flat or the NPC calling the bluff, not a normal refusal), Failed (the offer wasn't enough — a bigger offer might work, but not until something changes since this attempt is now remembered against them), LockedOut (bribery has already failed with this person over this; offering again, even more, will not help — some other approach or unrelated progress is needed first), Blocked (something else, unrelated to the bribe, is stopping them). If ReasonFact is present, the NPC also reveals that reason: narrate its Text faithfully. Otherwise do not invent a reason, supporting quest, or prerequisite. Revealing the reason does not disclose the primary fact."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of the person being bribed, copied verbatim from the most recent look or move result."
        )]
            string npcName,
        [Description("The amount of gold offered, as stated by the player.")] int goldOffered,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "[offer_bribe] npcName={NpcName} goldOffered={GoldOffered}",
            npcName,
            goldOffered
        );

        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = turnContext.PlayerId },
            cancellationToken
        );
        if (activeEncounter != null)
        {
            return new ToolError("An encounter is underway — resolve it before offering a bribe.");
        }

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = turnContext.PlayerId },
            cancellationToken
        );

        var npc = await getCreatureByNameAtLocation.Handle(
            new GetCreatureByNameAtLocationQuery
            {
                WorldId = turnContext.WorldId,
                LocationId = player!.LocationId,
                Name = npcName,
            },
            cancellationToken
        );
        if (npc == null)
        {
            return new ToolError(
                $"No one named '{npcName}' found nearby. Call look to see who's around."
            );
        }

        var objective = await getActiveObjectiveForNpc.Handle(
            new GetActiveLearnFactObjectiveForNpcQuery
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                NpcId = npc.Id,
            },
            cancellationToken
        );
        if (objective == null)
        {
            return new ToolError($"There's nothing {npcName} is withholding right now.");
        }

        var result = await offerBribeForFact.Handle(
            new OfferBribeForFactCommand
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                NpcId = npc.Id,
                FactId = objective.FactId,
                GoldOffered = goldOffered,
            },
            cancellationToken
        );

        logger.LogInformation("[offer_bribe] outcome={Outcome}", result.Outcome);

        return result;
    }
}
