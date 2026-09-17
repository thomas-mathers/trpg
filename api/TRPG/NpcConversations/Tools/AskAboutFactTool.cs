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
        "Presses someone to explain themselves when they're plainly withholding something. Call this whenever the player asks about the withheld subject or continues pressing it after a refusal, even when the follow-up uses conversational references instead of repeating the subject's name. After a refusal, remarks such as 'come on', 'you're holding something back', 'why won't you tell me?', or 'what would make you talk?' count as another attempt when that withheld subject is the current conversation topic. Do not call it for unrelated small talk, a change of subject, or a general request to improve the NPC's attitude. Use offer_bribe only for an explicit gold offer and intimidate only for an explicit threat. Resolved entirely by reputation and any relevant history between the player and this NPC; there is no random chance and no way to argue your way past it, so never decide the outcome yourself — narrate only what this returns. Outcome values: Disclosed (they told you — narrate FactText faithfully), Failed (they won't say, at least not like this — the player could try offering a bribe or intimidating them instead), Blocked (something else, unrelated to how you're asking, is stopping them). Call once for each player message that is a disclosure attempt, including a repeated attempt after Failed or Blocked. A later attempt may reveal why the NPC refuses even while the primary fact remains withheld. Never issue repeated calls for the same player message to force disclosure. If ReasonFact is present, the NPC also reveals that reason: narrate its Text faithfully. Otherwise do not invent a reason, supporting quest, or prerequisite. Revealing the reason does not disclose the primary fact."
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
