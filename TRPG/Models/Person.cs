namespace TRPG.Models;

internal class Person
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public string Name { get; init; } = "";
    public string Biography { get; set; } = "";
    public Guid RaceId { get; init; }
    public Guid BirthCityId { get; init; }
    public int BirthYear { get; init; }
    public Guid ProfessionId { get; init; }
    public Location Location { get; set; } = null!;
    public Progression Progression { get; set; } = null!;
    public Attributes Attributes { get; set; } = null!;
    public int Gold { get; set; }
}
