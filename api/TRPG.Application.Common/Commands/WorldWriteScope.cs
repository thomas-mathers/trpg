namespace TRPG.Application.Common.Commands;

public static class WorldWriteScope
{
    private static readonly AsyncLocal<Token?> Current = new();

    public static bool IsOpen => Current.Value is { IsDisposed: false };

    public static IDisposable Enter()
    {
        var token = new Token(Current.Value);
        Current.Value = token;
        return token;
    }

    private sealed class Token(Token? previous) : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
            Current.Value = previous;
        }
    }
}
