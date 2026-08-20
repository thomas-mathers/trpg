using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Generators;

internal record Shift(
    Profession Profession,
    IReadOnlyList<DayOfWeek> DaysOff,
    HourWindow WorkHours
);

internal record StaffingSchedule(Shift OwnerShift, IReadOnlyList<Shift> EmployeeShifts);

internal static class StaffingPolicy
{
    internal static readonly IReadOnlyList<DayOfWeek>[] NonOverlappingDayOffPatterns =
    [
        [DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday],
        [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday],
    ];

    internal static Profession GetProfessionForBuilding(BuildingType type)
    {
        return type switch
        {
            BuildingType.Tavern => Profession.Bartender,
            BuildingType.Blacksmith => Profession.Blacksmith,
            BuildingType.Temple => Profession.Cleric,
            BuildingType.Library => Profession.Scholar,
            BuildingType.GeneralGoods => Profession.Merchant,
            BuildingType.Apothecary => Profession.Alchemist,
            BuildingType.Bakery => Profession.Baker,
            BuildingType.Stable => Profession.StableMaster,
            BuildingType.ArcaneShop => Profession.Mage,
            BuildingType.GuildHall => Profession.Mercenary,
            BuildingType.Castle => Profession.Politician,
            BuildingType.Jail => Profession.Guard,
            BuildingType.Inn => Profession.Innkeeper,
            BuildingType.Barracks => Profession.Guard,
            BuildingType.Tailor => Profession.Tailor,
            BuildingType.Carpenter => Profession.Carpenter,
            BuildingType.Jeweler => Profession.Jeweler,
            _ => throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "No profession mapped for this building type."
            ),
        };
    }

    internal static Profession GetEmployeeProfessionForBuilding(BuildingType type)
    {
        return type == BuildingType.Castle ? Profession.Knight : GetProfessionForBuilding(type);
    }

    internal static HourWindow GetWorkHoursForBuilding(BuildingType type)
    {
        return type switch
        {
            BuildingType.Bakery => new HourWindow(6, 14),
            BuildingType.Tavern => new HourWindow(16, 4),
            BuildingType.Blacksmith or BuildingType.Carpenter or BuildingType.Stable =>
                new HourWindow(7, 17),
            BuildingType.ArcaneShop or BuildingType.Library => new HourWindow(9, 19),
            BuildingType.Castle or BuildingType.Jail or BuildingType.Barracks => new HourWindow(
                8,
                20
            ),
            _ => new HourWindow(8, 18),
        };
    }
}
