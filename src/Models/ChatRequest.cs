
/// <param name="Input">The user's message for this turn.</param>
/// <param name="SessionId">
/// Optional identifier for the conversation session.  When supplied, prior turns
/// in the session are included in the agent context so the agent can reference them.
/// When omitted, the endpoint generates a new session ID automatically — the returned
/// <c>sessionId</c> field can then be passed back on subsequent requests to continue
/// the conversation.
/// </param>
public record ChatRequest(string Input, string? SessionId = null);