namespace TRPG.Application.Common.Commands;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ExemptFromWorldWriteGuardAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
