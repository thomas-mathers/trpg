using TRPG.Abilities.Mappers;
using TRPG.GameSessions.Mappers;
using AbilitiesConditionType = TRPG.Application.Abilities.ConditionType;
using AmountTypeMapper = TRPG.Application.Combat.Mappers.AmountTypeMapper;
using AttributeNameMapper = TRPG.Application.Combat.Mappers.AttributeNameMapper;
using ConditionTypeMapper = TRPG.Application.Combat.Mappers.ConditionTypeMapper;
using DamageTypeMapper = TRPG.Application.Combat.Mappers.DamageTypeMapper;
using DataAmountType = TRPG.Domain.Models.AmountType;
using DataAttributeName = TRPG.Domain.Models.AttributeName;
using DataBuildingType = TRPG.Domain.Models.BuildingType;
using DataCreatureState = TRPG.Domain.Models.CreatureState;
using DataCreatureType = TRPG.Domain.Models.CreatureType;
using DataDamageType = TRPG.Domain.Models.DamageType;
using DataDistrictType = TRPG.Domain.Models.DistrictType;
using DataEquipmentSlot = TRPG.Domain.Models.EquipmentSlot;
using DataGender = TRPG.Domain.Models.Gender;
using DataItemRarity = TRPG.Domain.Models.ItemRarity;
using DataProfession = TRPG.Domain.Models.Profession;
using DataSkill = TRPG.Domain.Models.Skill;
using EquipmentSlotMapper = TRPG.Creatures.Mappers.EquipmentSlotMapper;
using ItemRarityMapper = TRPG.Creatures.Mappers.ItemRarityMapper;

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
        var result = DamageTypeMapper.ToContract(value);

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(AttributeNameValues))]
    public void ToContract_MapsAttributeNameByName(DataAttributeName value)
    {
        // Act
        var result = AttributeNameMapper.ToContract(value);

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(AmountTypeValues))]
    public void ToContract_MapsAmountTypeByName(DataAmountType value)
    {
        // Act
        var result = AmountTypeMapper.ToContract(value);

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
        var result = ConditionTypeMapper.ToContract(value);

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(ItemRarityValues))]
    public void ToContract_MapsItemRarityByName(DataItemRarity value)
    {
        // Act
        var result = ItemRarityMapper.ToContract(value);

        // Assert
        Assert.Equal(value.ToString(), result.ToString());
    }

    [Theory]
    [MemberData(nameof(EquipmentSlotValues))]
    public void ToContract_MapsEquipmentSlotByName(DataEquipmentSlot value)
    {
        // Act
        var result = EquipmentSlotMapper.ToContract(value);

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

    public static IEnumerable<object[]> AmountTypeValues() => AllValues<DataAmountType>();

    public static IEnumerable<object[]> SkillValues() => AllValues<DataSkill>();

    public static IEnumerable<object[]> ConditionTypeValues() =>
        AllValues<AbilitiesConditionType>();

    public static IEnumerable<object[]> ItemRarityValues() => AllValues<DataItemRarity>();

    public static IEnumerable<object[]> EquipmentSlotValues() => AllValues<DataEquipmentSlot>();

    private static IEnumerable<object[]> AllValues<TEnum>()
        where TEnum : struct, Enum => Enum.GetValues<TEnum>().Select(v => new object[] { v });
}
