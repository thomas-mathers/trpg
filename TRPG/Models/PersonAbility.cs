namespace TRPG.Models;

internal class PersonAbility {
    public string AbilityName { get; init; } = "";
    public int Cooldown { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; init; }
}