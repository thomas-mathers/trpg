namespace TRPG.Application.Common.Navigation;

public static class NavigationCacheKeys
{
    public static string Grid(Guid locationId) => $"navigationGrid:{locationId}";
}
