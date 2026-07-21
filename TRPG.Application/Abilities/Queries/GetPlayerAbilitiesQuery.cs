using TRPG.Application.Creatures.Queries;

namespace TRPG.Application.Abilities.Queries;

internal class GetPlayerAbilitiesQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class GetPlayerAbilitiesQueryHandler(
    GetCreatureAbilitiesQueryHandler getCreatureAbilities,
    AbilityDefinitions abilityDefinitions
)
{
    public async Task<IReadOnlyCollection<Ability>> Handle(
        GetPlayerAbilitiesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var learnedNames = await getCreatureAbilities.Handle(
            new GetCreatureAbilitiesQuery { WorldId = query.WorldId, CreatureId = query.PlayerId },
            cancellationToken
        );

        var learnedAbilities = learnedNames
            .Select(abilityDefinitions.GetByName)
            .OfType<Ability>();

        return [abilityDefinitions.BasicAttack, abilityDefinitions.BlockStance, .. learnedAbilities];
    }
}
