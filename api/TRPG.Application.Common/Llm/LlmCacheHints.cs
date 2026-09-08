namespace TRPG.Application.Common.Llm;

// A caller marks where its stable prefix ends; the host decides what that means for the provider
// behind IChatClient, so a module never has to name one.
public static class LlmCacheHints
{
    public const string PrefixEnd = "trpg:cache_prefix_end";
}
