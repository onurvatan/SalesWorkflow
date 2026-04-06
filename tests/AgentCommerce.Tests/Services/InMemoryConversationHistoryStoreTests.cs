using Microsoft.Extensions.AI;
using SalesWorkflow.Services;
using Xunit;

namespace SalesWorkflow.Tests.Services;

public class InMemoryConversationHistoryStoreTests
{
    private static InMemoryConversationHistoryStore CreateSut() => new();

    // ── GetOrCreate ────────────────────────────────────────────────────────

    [Fact]
    public void GetOrCreate_UnknownSession_ReturnsEmptyList()
    {
        var sut = CreateSut();

        var result = sut.GetOrCreate("unknown-session");

        Assert.Empty(result);
    }

    [Fact]
    public void GetOrCreate_AfterSave_ReturnsSavedMessages()
    {
        var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello"),
            new(ChatRole.Assistant, "Hi there!")
        };

        sut.Save("session-1", messages);
        var result = sut.GetOrCreate("session-1");

        Assert.Equal(2, result.Count);
        Assert.Equal("Hello", result[0].Text);
        Assert.Equal("Hi there!", result[1].Text);
    }

    [Fact]
    public void GetOrCreate_ReturnsCopy_NotReference()
    {
        var sut = CreateSut();
        sut.Save("session-1", [new(ChatRole.User, "A")]);

        var first = sut.GetOrCreate("session-1");
        first.Add(new ChatMessage(ChatRole.User, "mutated"));

        // Second fetch should not contain the mutation
        var second = sut.GetOrCreate("session-1");
        Assert.Single(second);
    }

    // ── Save ───────────────────────────────────────────────────────────────

    [Fact]
    public void Save_OverwritesPriorHistory()
    {
        var sut = CreateSut();
        sut.Save("session-1", [new(ChatRole.User, "first")]);
        sut.Save("session-1", [new(ChatRole.User, "replaced")]);

        var result = sut.GetOrCreate("session-1");

        Assert.Single(result);
        Assert.Equal("replaced", result[0].Text);
    }

    [Fact]
    public void Save_StoresDefensiveCopy()
    {
        var sut = CreateSut();
        var original = new List<ChatMessage> { new(ChatRole.User, "original") };
        sut.Save("session-1", original);

        // Mutate the list after saving — should not affect stored history
        original.Add(new ChatMessage(ChatRole.User, "injected"));

        var result = sut.GetOrCreate("session-1");
        Assert.Single(result);
    }

    // ── Delete ─────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_ExistingSession_ReturnsTrueAndRemoves()
    {
        var sut = CreateSut();
        sut.Save("session-1", [new(ChatRole.User, "hello")]);

        var deleted = sut.Delete("session-1");

        Assert.True(deleted);
        Assert.Empty(sut.GetOrCreate("session-1"));
    }

    [Fact]
    public void Delete_UnknownSession_ReturnsFalse()
    {
        var sut = CreateSut();

        var result = sut.Delete("does-not-exist");

        Assert.False(result);
    }

    [Fact]
    public void Delete_CalledTwice_ReturnsFalseOnSecondCall()
    {
        var sut = CreateSut();
        sut.Save("session-1", [new(ChatRole.User, "hello")]);

        sut.Delete("session-1");
        var second = sut.Delete("session-1");

        Assert.False(second);
    }

    // ── Multiple sessions ─────────────────────────────────────────────────

    [Fact]
    public void MultipleSessionsAreIsolated()
    {
        var sut = CreateSut();
        sut.Save("session-A", [new(ChatRole.User, "A message")]);
        sut.Save("session-B", [new(ChatRole.User, "B message")]);

        sut.Delete("session-A");

        Assert.Empty(sut.GetOrCreate("session-A"));
        Assert.Single(sut.GetOrCreate("session-B"));
    }
}
