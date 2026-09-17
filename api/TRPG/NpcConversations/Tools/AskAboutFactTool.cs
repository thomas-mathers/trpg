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

internal class AskAboutFactTool(
    GameTurnContext turnContext,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreatureByNameAtLocationQuery, Creature?> getCreatureByNameAtLocation,
    IQueryHandler<
        GetActiveLearnFactObjectiveForNpcQuery,
        LearnFactFromCreatureObjective?
    > getActiveObjectiveForNpc,
    ICommandHandler<AskAboutFactCommand, FactDisclosureResult> askAboutFact,
    ILogger<AskAboutFactTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("ask_about_fact")]
    [Description(
        "Presses someone to explain themselves when they're plainly withholding something — call this when the player directly asks the question the NPC is dodging, not for ordinary conversation. Resolved entirely by reputation and any relevant history between the player and this NPC; there is no random chance and no way to argue your way past it, so never decide the outcome yourself — narrate only what this returns. Outcome values: Disclosed (they told you — narrate FactText faithfully), Failed (they won't say, at least not like this — the player could try offering a bribe or intimidating them instead), Blocked (something else is stopping them, unrelated to how you're asking). Calling this again immediately after Failed will not change the outcome unless something about the relationship has genuinely changed."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of the person being asked, copied verbatim from the most recent look or move result."
        )]
            string npcName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[ask_about_fact] npcName={NpcName}", npcName);

        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = turnContext.PlayerId },
            cancellationToken
        );
        if (activeEncounter != null)
        {
            return new ToolError(
                "An encounter is underway — resolve it before pressing anyone for information."
            );
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

        var result = await askAboutFact.Handle(
            new AskAboutFactCommand
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                NpcId = npc.Id,
                FactId = objective.FactId,
            },
            cancellationToken
        );

        logger.LogInformation("[ask_about_fact] outcome={Outcome}", result.Outcome);

        return result;
    }
}
