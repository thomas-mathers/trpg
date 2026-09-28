namespace TRPG.Domain.Models;

public enum CreatureJobAction
{
    Sleep,
    Work,
    Idle,
    Study,
    Pray,
    Eat,
}

public class CreatureJob
{
    public CreatureJobAction Action { get; init; }
    public Guid CreatureId { get; init; }
    public int EndHour { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid LocationId { get; init; }
    public int Priority { get; init; }
    public Guid? RouteId { get; init; }
    public DayOfWeek? SpecificDay { get; init; }
    public int StartHour { get; init; }
    public Guid WorldId { get; init; }

    public CreatureActivity? Activity => Action.ToActivity();
}

public static class CreatureJobActionExtensions
{
    public static CreatureActivity? ToActivity(this CreatureJobAction action) =>
        action switch
        {
            CreatureJobAction.Sleep => null,
            CreatureJobAction.Idle => null,
            CreatureJobAction.Work => CreatureActivity.Working,
            CreatureJobAction.Study => CreatureActivity.Studying,
            CreatureJobAction.Pray => CreatureActivity.Praying,
            CreatureJobAction.Eat => CreatureActivity.Eating,
            _ => throw new InvalidOperationException("Unknown creature job action."),
        };
}
