using Microsoft.Extensions.AI;

namespace SalesWorkflow.Services;

/// <summary>
/// Stores and retrieves per-session conversation history as a flat list of
/// <see cref="ChatMessage"/> values in the order they were exchanged.
/// </summary>
/// <remarks>
/// The default implementation (<see cref="InMemoryConversationHistoryStore"/>) uses a
/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/> and is
/// suitable for development and single-instance deployments.
/// For production or multi-instance scenarios, replace the DI registration with a
/// store backed by Redis, Azure Cosmos DB, SQL Server, or similar persistent storage.
/// </remarks>
public interface IConversationHistoryStore
{
    /// <summary>
    /// Returns the stored messages for <paramref name="sessionId"/>, or an empty list if
    /// no session exists yet. The returned list may be mutated by the caller; call
    /// <see cref="Save"/> to persist changes.
    /// </summary>
    List<ChatMessage> GetOrCreate(string sessionId);

    /// <summary>Overwrites the stored history for <paramref name="sessionId"/>.</summary>
    void Save(string sessionId, List<ChatMessage> messages);

    /// <summary>
    /// Removes the session. Returns <see langword="true"/> if the session existed,
    /// <see langword="false"/> if it was not found.
    /// </summary>
    bool Delete(string sessionId);
}
