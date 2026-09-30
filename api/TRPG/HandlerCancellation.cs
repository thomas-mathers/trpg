namespace TRPG;

internal static class HandlerCancellation
{
    public static bool IsCallerCancellation(
        Exception exception,
        CancellationToken cancellationToken
    ) => exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
}
