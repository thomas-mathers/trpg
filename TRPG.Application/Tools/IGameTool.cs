namespace TRPG.Application.Tools;

internal interface IGameTool
{
    Delegate Invoke { get; }
}
