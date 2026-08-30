namespace TRPG.Domain.Models;

public class RoomBooking
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid RoomId { get; init; }
    public Guid KeyItemId { get; init; }
    public Guid PlayerId { get; init; }
    public TimeSpan DueAtPlaytime { get; init; }
}
