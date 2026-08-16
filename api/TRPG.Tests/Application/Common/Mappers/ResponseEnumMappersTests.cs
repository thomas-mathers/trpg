using TRPG.Abilities.Mappers;
using TRPG.GameSessions.Mappers;
using AbilitiesConditionType = TRPG.Application.Abilities.ConditionType;
using CombatResponseEnumMappers = TRPG.Application.Combat.Mappers.CombatResponseEnumMappers;
using DataAmountType = TRPG.Data.Models.AmountType;
using DataAttributeName = TRPG.Data.Models.AttributeName;
using DataBuildingType = TRPG.Data.Models.BuildingType;
using DataCreatureState = TRPG.Data.Models.CreatureState;
using DataCreatureType = TRPG.Data.Models.CreatureType;
using DataDamageType = TRPG.Data.Models.DamageType;
using DataDistrictType = TRPG.Data.Models.DistrictType;
using DataEquipmentSlot = TRPG.Data.Models.EquipmentSlot;
using DataGender = TRPG.Data.Models.Gender;
using DataItemRarity = TRPG.Data.Models.ItemRarity;
using DataProfession = TRPG.Data.Models.Profession;
using DataResourceType = TRPG.Data.Models.ResourceType;
using DataSkill = TRPG.Data.Models.Skill;
using ItemResponseEnumMappers = TRPG.Creatures.Mappers.ItemResponseEnumMappers;

namespace TRPG.Tests.Application.Common.Mappers;

public class ResponseEnumMappersTests
{
    [Theory]
    [MemberData(nameof(GenderValues))]
    public void ToContract_MapsGenderByName(DataGender value)
    {
        // Act
        var result = value.ToContract();

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(CreatureTypeValues))]
    public void ToContract_MapsCreatureTypeByName(DataCreatureType value)
    {
        // Act
        var result = value.ToContract();

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(ProfessionValues))]
    public void ToContract_MapsProfessionByName(DataProfession value)
    {
        // Act
        var result = value.ToContract();

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(CreatureStateValues))]
    public void ToContract_MapsCreatureStateByName(DataCreatureState value)
    {
        // Act
        var result = value.ToContract();

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(DistrictTypeValues))]
    public void ToContract_MapsDistrictTypeByName(DataDistrictType value)
    {
        // Act
        var result = value.ToContract();

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(BuildingTypeValues))]
    public void ToContract_MapsBuildingTypeByName(DataBuildingType value)
    {
        // Act
        var result = value.ToContract();

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(DamageTypeValues))]
    public void ToContract_MapsDamageTypeByName(DataDamageType value)
    {
        // Act
        var result = CombatResponseEnumMappers.ToContract(value);

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(AttributeNameValues))]
    public void ToContract_MapsAttributeNameByName(DataAttributeName value)
    {
        // Act
        var result = CombatResponseEnumMappers.ToContract(value);

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(ResourceTypeValues))]
    public void ToContract_MapsResourceTypeByName(DataResourceType value)
    {
        // Act
        var result = CombatResponseEnumMappers.ToContract(value);

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(AmountTypeValues))]
    public void ToContract_MapsAmountTypeByName(DataAmountType value)
    {
        // Act
        var result = CombatResponseEnumMappers.ToContract(value);

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(SkillValues))]
    public void ToContract_MapsSkillByName(DataSkill value)
    {
        // Act
        var result = value.ToContract();

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(ConditionTypeValues))]
    public void ToContract_MapsConditionTypeByName(AbilitiesConditionType value)
    {
        // Act
        var result = CombatResponseEnumMappers.ToContract(value);

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(ItemRarityValues))]
    public void ToContract_MapsItemRarityByName(DataItemRarity value)
    {
        // Act
        var result = ItemResponseEnumMappers.ToContract(value);

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(EquipmentSlotValues))]
    public void ToContract_MapsEquipmentSlotByName(DataEquipmentSlot value)
    {
        // Act
        var result = ItemResponseEnumMappers.ToContract(value);

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    public static IEnumerable<object[]> GenderValues() => AllValues<DataGender>();

    public static IEnumerable<object[]> CreatureTypeValues() => AllValues<DataCreatureType>();

    public static IEnumerable<object[]> ProfessionValues() => AllValues<DataProfession>();

    public static IEnumerable<object[]> CreatureStateValues() => AllValues<DataCreatureState>();

    public static IEnumerable<object[]> DistrictTypeValues() => AllValues<DataDistrictType>();

    public static IEnumerable<object[]> BuildingTypeValues() => AllValues<DataBuildingType>();

    public static IEnumerable<object[]> DamageTypeValues() => AllValues<DataDamageType>();

    public static IEnumerable<object[]> AttributeNameValues() => AllValues<DataAttributeName>();

    public static IEnumerable<object[]> ResourceTypeValues() => AllValues<DataResourceType>();

    public static IEnumerable<object[]> AmountTypeValues() => AllValues<DataAmountType>();

    public static IEnumerable<object[]> SkillValues() => AllValues<DataSkill>();

    public static IEnumerable<object[]> ConditionTypeValues() =>
        AllValues<AbilitiesConditionType>();

    public static IEnumerable<object[]> ItemRarityValues() => AllValues<DataItemRarity>();

    public static IEnumerable<object[]> EquipmentSlotValues() => AllValues<DataEquipmentSlot>();

    private static IEnumerable<object[]> AllValues<TEnum>()
        where TEnum : struct, Enum => Enum.GetValues<TEnum>().Select(v => new object[] { v });
}
