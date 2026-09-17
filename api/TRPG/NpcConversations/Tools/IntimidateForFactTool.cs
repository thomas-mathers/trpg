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

internal class IntimidateForFactTool(
    GameTurnContext turnContext,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreatureByNameAtLocationQuery, Creature?> getCreatureByNameAtLocation,
    IQueryHandler<
        GetActiveLearnFactObjectiveForNpcQuery,
        LearnFactFromCreatureObjective?
    > getActiveObjectiveForNpc,
    ICommandHandler<IntimidateForFactCommand, FactDisclosureResult> intimidateForFact,
    ILogger<IntimidateForFactTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("intimidate")]
    [Description(
        "Threatens someone into explaining what they're withholding — call this only when the player explicitly threatens or menaces the NPC, not for ordinary firmness. This is not a fight; nothing is triggered beyond the NPC's reaction. Never decide the outcome yourself — narrate only what this returns. Outcome values: Disclosed (the threat worked — narrate FactText faithfully), Failed (they held their nerve this time — pressing harder right now will not help, something needs to change first), LockedOut (intimidation has already failed with this person over this; trying again will not help until something else changes), TooWeak (this player poses no credible threat to someone this capable — describe the NPC as openly unbothered or amused, not merely refusing; do not treat this as a normal failure), Blocked (something else, unrelated to the threat, is stopping them — MissingRequiredQuestNames names what still needs doing; have the NPC hint at it in character rather than reciting the quest name mechanically)."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of the person being threatened, copied verbatim from the most recent look or move result."
        )]
            string npcName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[intimidate] npcName={NpcName}", npcName);

        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = turnContext.PlayerId },
            cancellationToken
        );
        if (activeEncounter != null)
        {
            return new ToolError(
                "An encounter is underway — resolve it before threatening anyone."
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

        var result = await intimidateForFact.Handle(
            new IntimidateForFactCommand
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                NpcId = npc.Id,
                FactId = objective.FactId,
            },
            cancellationToken
        );

        logger.LogInformation("[intimidate] outcome={Outcome}", result.Outcome);

        return result;
    }
}
