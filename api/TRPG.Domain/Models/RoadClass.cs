namespace TRPG.Domain.Models;

public enum RoadClass
{
    Avenue,
    Street,
    Lane,
}

public static class RoadClassWidths
{
    public static double Of(RoadClass roadClass) =>
        roadClass switch
        {
            RoadClass.Avenue => 4.5,
            RoadClass.Street => 3.0,
            _ => 1.5,
        };
}
