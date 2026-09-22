namespace TRPG.Domain.Models;

// A sign whose displayed text is computed live from current caravan positions rather than the
// stored Description — see the /signs/{signId} endpoint's dispatch on this type.
public class CaravanScheduleSign : Sign;
