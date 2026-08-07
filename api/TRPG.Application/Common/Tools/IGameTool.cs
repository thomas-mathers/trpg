namespace TRPG.Application.Common.Tools;

internal interface IGameTool
{
    Delegate Invoke { get; }
}
