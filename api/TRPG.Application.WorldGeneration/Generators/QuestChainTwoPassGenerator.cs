using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Extensions;

namespace TRPG.Application.WorldGeneration.Generators;

// Wire schema for the free-form narrative-planning pass: a single prose field, not the DAG shape —
// deliberately loose so the model can write the whole quest's story before being asked to formalize
// it into PrerequisiteNodeIds/GroupIndex/objective fields in the second pass.
internal class QuestChainDescriptionSchema
{
    public string Description { get; init; } = "";
}

// An alternate QuestChainGeneratorInput -> QuestChainGeneratedResult implementation, kept alongside
// QuestChainGenerator (not replacing it) so the two can be compared directly: QuestChainGenerator
// authors narrative and DAG topology simultaneously in one completion; this generator splits that
// into a free-form narrative-planning pass followed by a second pass that formalizes the resulting
// plan into the exact same schema/rules QuestChainGenerator enforces, so any difference in output
// validity comes from the two-pass split itself rather than from different mechanical constraints.
public class QuestChainTwoPassGenerator(
    [FromKeyedServices(LlmRoleKeys.QuestGeneration)] IChatClient client,
    ILogger<QuestChainTwoPassGenerator> logger
)
{
    public async Task<QuestChainGeneratedResult> Generate(
        QuestChainGeneratorInput input,
        CancellationToken cancellationToken
    )
    {
        var entityList = QuestChainGenerator.BuildEntityListing(input.AvailableEntities);

        var description = await GenerateDescription(input, entityList, cancellationToken);
        var schema = await GenerateStructure(input, entityList, description, cancellationToken);

        return QuestChainGenerator.MapToResult(schema);
    }

    private async Task<string> GenerateDescription(
        QuestChainGeneratorInput input,
        string entityList,
        CancellationToken cancellationToken
    )
    {
        var systemPrompt = $"""
            You are planning an entire quest chain for a text RPG as a complete narrative outline —
            prose, not JSON structure. Do not think about node ids, prerequisites, or schema fields
            yet; a later pass will formalize whatever you write here into that structure.

            Write out the whole chain as a sequence of roughly {input.ChainLength} distinct quest
            beats. For each beat, describe: what happens, which entity from the list below gives it
            and which entities (if any) it targets, and what the player must concretely do (fight,
            explore, talk to someone, retrieve or deliver something, or learn something someone is
            reluctant to share). Reference entities by the exact name given below wherever a beat
            involves one, so they can be mapped back to their ids later.

            Include:
            - A clear inciting beat that hooks the player into the premise.
            - At least one moment where the story branches into distinct alternative approaches
              (e.g. bribery, stealth, force, diplomacy, or backing a different ally) that later
              reconverge on a shared outcome.
            - At most one secret an NPC initially withholds, why they refuse to share it, and what
              would concretely change their mind.
            - A resolution beat that closes out the central conflict.

            Respond with only raw JSON matching the schema. No markdown, no commentary.
            """;

        var userPrompt = $"""
            Chain premise:
            {input.ChainPremise}

            Available entities:
            {entityList}
            """;

        var result = await client.GetValidatedJson<QuestChainDescriptionSchema>(
            logger,
            systemPrompt,
            userPrompt,
            schema =>
                string.IsNullOrWhiteSpace(schema.Description)
                    ? "Description must not be blank."
                    : null,
            cancellationToken,
            options: new ChatOptions { MaxOutputTokens = 4096 }
        );

        return result.Description;
    }

    private async Task<QuestChainSchema> GenerateStructure(
        QuestChainGeneratorInput input,
        string entityList,
        string description,
        CancellationToken cancellationToken
    )
    {
        var entityTypesById = QuestChainGenerator.BuildEntityTypesById(input.AvailableEntities);
        var systemPrompt = QuestChainGenerator.BuildSystemPrompt(input.ChainLength);

        var userPrompt = $"""
            Chain premise:
            {input.ChainPremise}

            Narrative plan to formalize into the DAG structure described above — follow its beats,
            branches, and resolution as closely as the mechanical rules allow, adapting only where a
            rule requires it:
            {description}

            Available entities:
            {entityList}
            """;

        return await client.GetValidatedJson<QuestChainSchema>(
            logger,
            systemPrompt,
            userPrompt,
            schema => QuestChainGenerator.Validate(schema, entityTypesById, input.ChainLength),
            cancellationToken,
            // Same sizing rationale as QuestChainGenerator's single-pass call — a multi-node DAG
            // with several objectives per node runs long enough to get cut off mid-node otherwise.
            options: new ChatOptions { MaxOutputTokens = 8192 }
        );
    }
}
