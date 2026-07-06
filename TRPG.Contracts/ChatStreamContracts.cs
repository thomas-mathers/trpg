using System.Text.Json.Serialization;

namespace TRPG.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ChatStreamToken), "token")]
[JsonDerivedType(typeof(ChatStreamDone), "done")]
[JsonDerivedType(typeof(ChatStreamScene), "scene")]
public abstract record ChatStreamMessage;

public sealed record ChatStreamToken(string Text) : ChatStreamMessage;

public sealed record ChatStreamDone : ChatStreamMessage;

public sealed record ChatStreamScene(SceneSnapshot Scene) : ChatStreamMessage;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ChatCommand), "chat")]
[JsonDerivedType(typeof(WaitCommand), "wait")]
public abstract record ClientCommand;

public sealed record ChatCommand(string Message) : ClientCommand;

public sealed record WaitCommand(int Hours) : ClientCommand;
