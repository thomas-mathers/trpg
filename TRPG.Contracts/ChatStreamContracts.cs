using System.Text.Json.Serialization;

namespace TRPG.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ChatStreamToken), "token")]
[JsonDerivedType(typeof(ChatStreamDone), "done")]
public abstract record ChatStreamMessage;

public sealed record ChatStreamToken(string Text) : ChatStreamMessage;

public sealed record ChatStreamDone : ChatStreamMessage;
