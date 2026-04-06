using Microsoft.Extensions.AI;
using System.Collections.Concurrent;

namespace SalesWorkflow.Services;

/// <summary>
/// In-memory implementation of <see cref="IConversationHistoryStore"/>.
/// </summary>
/// <remarks>
/// Uses a <see cref="ConcurrentDictionary{TKey,TValue}"/> so concurrent requests on
/// different sessions are safe without explicit locking.  Concurrent requests on the
/// <em>same</em> session are serialised naturally by the HTTP layer (one request at a time
/// per session ID in normal chat usage).
/// <para>
/// Data is lost on process restart.  For production deployments, replace the DI
/// registration in <c>ServiceCollectionExtensions.AddCommonServices</c> with an
/// implementation backed by Redis, Azure Cosmos DB, or SQL Server:
/// <code>builder.Services.AddSingleton&lt;IConversationHistoryStore, RedisConversationHistoryStore&gt;();</code>
/// </para>
/// </remarks>
public sealed class InMemoryConversationHistoryStore : IConversationHistoryStore
{
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _sessions = new();

    /// <inheritdoc/>
    public List<ChatMessage> GetOrCreate(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var existing)
            ? [.. existing]          // return a copy so the caller owns the list
            : [];

    /// <inheritdoc/>
    public void Save(string sessionId, List<ChatMessage> messages) =>
        _sessions[sessionId] = [.. messages];   // store a defensive copy

    /// <inheritdoc/>
    public bool Delete(string sessionId) =>
        _sessions.TryRemove(sessionId, out _);
}
