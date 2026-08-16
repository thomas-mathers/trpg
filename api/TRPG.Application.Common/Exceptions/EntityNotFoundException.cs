namespace TRPG.Application.Common.Exceptions;

public class EntityNotFoundException(string entityName, Guid entityId)
    : Exception($"{entityName} {entityId} not found.");
