namespace TRPG.Application.Common.Exceptions;

public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string entityName)
        : base($"{entityName} not found.") { }

    public EntityNotFoundException(string entityName, Guid entityId)
        : base($"{entityName} {entityId} not found.") { }
}
