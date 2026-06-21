namespace TRPG.Models;

internal class FactionMember
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; init; }
    public Guid FactionId { get; init; }
    public FactionRole Role { get; set; }
}
