using TRPG.Application.Creatures.Queries;

namespace TRPG.Application.Abilities.Queries;

internal class GetUsableAbilitiesQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetUsableAbilitiesQueryHandler(
    GetCreatureAbilitiesQueryHandler getCreatureAbilities,
    AbilityDefinitions abilityDefinitions
)
{
    public async Task<IReadOnlyCollection<Ability>> Handle(
        GetUsableAbilitiesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var learnedNames = await getCreatureAbilities.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = query.CreatureId },
            cancellationToken
        );

        var learnedAbilities = learnedNames.Select(abilityDefinitions.GetByName).OfType<Ability>();

        return
        [
            abilityDefinitions.BasicAttack,
            abilityDefinitions.BlockStance,
            .. learnedAbilities,
        ];
    }
}
