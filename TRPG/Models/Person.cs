namespace TRPG.Models;

internal class Person {
    public Attributes Attributes { get; set; } = null!;
    public string Biography { get; set; } = "";
    public Guid BirthCityId { get; init; }
    public int BirthYear { get; init; }
    public int Gold { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Location Location { get; set; } = null!;
    public string Name { get; init; } = "";
    public Guid ProfessionId { get; init; }
    public Progression Progression { get; set; } = null!;
    public Guid RaceId { get; init; }
    public Guid WorldId { get; init; }
}