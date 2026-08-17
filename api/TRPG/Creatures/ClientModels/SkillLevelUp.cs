using Tapper;
using TRPG.Abilities.Responses;
using TypedSignalR.Client;

namespace TRPG.Creatures.ClientModels;

[TranspilationSource]
public record SkillLevelUp(
    Skill Skill,
    int Level,
    int CharacterExperienceCurrent,
    int CharacterExperienceToNextLevel
);
