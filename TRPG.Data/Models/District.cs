namespace TRPG.Data.Models;

public enum DistrictType
{
    Residential,
    Scientific,
    CityCenter,
    Governmental,
    HolySite,
    Encampment,
}

public class District
{
    public Guid CityId { get; init; }
    public string Description { get; init; } = "";
    public DistrictType DistrictType { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid WorldId { get; init; }
}
