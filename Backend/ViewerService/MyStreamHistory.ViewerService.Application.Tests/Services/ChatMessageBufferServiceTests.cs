using MyStreamHistory.ViewerService.Application.Services;
using Xunit;

namespace MyStreamHistory.ViewerService.Application.Tests.Services;

public class ChatMessageBufferServiceTests
{
    [Fact]
    public void CreateSnapshot_PausedStream_DoesNotAccrueOrRetainMessages()
    {
        var service = new ChatMessageBufferService();
        var sessionId = Guid.NewGuid();

        service.InitializeStream("streamer", sessionId, Guid.NewGuid());
        service.AddChatMessage("streamer", "viewer", 50);
        service.PauseStream("streamer", sessionId);

        Assert.False(service.IsAccrualEnabled("streamer", sessionId));
        Assert.Empty(service.CreateSnapshot().StreamSnapshots);

        service.ResumeStream("streamer", sessionId);
        var resumedSnapshot = service.CreateSnapshot();

        Assert.True(resumedSnapshot.StreamSnapshots.TryGetValue("streamer", out var stream));
        Assert.True(service.IsAccrualEnabled("streamer", sessionId));
        Assert.Empty(stream!.ChatMessages);
    }

    [Fact]
    public void PauseStream_EventForDifferentSession_DoesNotPauseCurrentStream()
    {
        var service = new ChatMessageBufferService();
        var currentSessionId = Guid.NewGuid();

        service.InitializeStream("streamer", currentSessionId, Guid.NewGuid());
        service.PauseStream("streamer", Guid.NewGuid());

        Assert.Contains("streamer", service.CreateSnapshot().StreamSnapshots.Keys);
    }

    [Fact]
    public void ResumeStream_RepeatedConfirmation_DoesNotClearActiveMessages()
    {
        var service = new ChatMessageBufferService();
        var sessionId = Guid.NewGuid();

        service.InitializeStream("streamer", sessionId, Guid.NewGuid());
        service.AddChatMessage("streamer", "viewer", 25);
        service.ResumeStream("streamer", sessionId);

        var stream = service.CreateSnapshot().StreamSnapshots["streamer"];
        Assert.Equal(25, stream.ChatMessages["viewer"]);
    }

    [Fact]
    public void RemoveStream_EventForOldSession_DoesNotRemoveCurrentStream()
    {
        var service = new ChatMessageBufferService();
        var currentSessionId = Guid.NewGuid();
        service.InitializeStream("streamer", currentSessionId, Guid.NewGuid());

        var removed = service.RemoveStream("streamer", Guid.NewGuid());

        Assert.False(removed);
        Assert.True(service.IsStreamActive("streamer", currentSessionId));
    }

    [Fact]
    public void UpdateStreamCategory_EventForOldSession_DoesNotChangeCurrentCategory()
    {
        var service = new ChatMessageBufferService();
        var currentSessionId = Guid.NewGuid();
        var currentCategoryId = Guid.NewGuid();
        service.InitializeStream("streamer", currentSessionId, currentCategoryId);

        var updated = service.UpdateStreamCategory("streamer", Guid.NewGuid(), Guid.NewGuid());
        var snapshot = service.CreateSnapshot().StreamSnapshots["streamer"];

        Assert.False(updated);
        Assert.Equal(currentCategoryId, snapshot.CurrentCategoryId);
    }
}
