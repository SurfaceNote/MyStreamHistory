using System.Collections.Concurrent;
using MyStreamHistory.ViewerService.Application.DTOs;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Application.Services;

public class ChatMessageBufferService : IChatMessageBufferService
{
    private readonly ConcurrentDictionary<string, StreamBuffer> _streamBuffers = new();

    public void InitializeStream(string twitchUserId, Guid streamSessionId, Guid? currentCategoryId)
    {
        _streamBuffers[twitchUserId] = new StreamBuffer
        {
            StreamSessionId = streamSessionId,
            CurrentCategoryId = currentCategoryId,
            ChatMessages = new ConcurrentDictionary<string, int>()
        };
    }

    public void AddChatMessage(string twitchUserId, string chatterUserId, int characterCount)
    {
        if (!_streamBuffers.TryGetValue(twitchUserId, out var buffer))
            return;

        buffer.ChatMessages.AddOrUpdate(chatterUserId, characterCount, (_, existing) => existing + characterCount);
    }

    public void UpdateStreamCategory(string twitchUserId, Guid newCategoryId)
    {
        if (_streamBuffers.TryGetValue(twitchUserId, out var buffer))
        {
            buffer.CurrentCategoryId = newCategoryId;
        }
    }

    public DataCollectionSnapshot CreateSnapshot()
    {
        var snapshot = new DataCollectionSnapshot
        {
            Timestamp = DateTime.UtcNow,
            StreamSnapshots = new Dictionary<string, StreamDataSnapshot>()
        };

        foreach (var (twitchUserId, buffer) in _streamBuffers)
        {
            // Create a snapshot and clear the buffer for next minute
            var chatMessages = buffer.ChatMessages.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            buffer.ChatMessages.Clear();

            snapshot.StreamSnapshots[twitchUserId] = new StreamDataSnapshot
            {
                TwitchUserId = twitchUserId,
                StreamSessionId = buffer.StreamSessionId,
                CurrentCategoryId = buffer.CurrentCategoryId,
                ChatMessages = chatMessages
            };
        }

        return snapshot;
    }

    public void RemoveStream(string twitchUserId)
    {
        _streamBuffers.TryRemove(twitchUserId, out _);
    }

    private class StreamBuffer
    {
        public Guid StreamSessionId { get; set; }
        public Guid? CurrentCategoryId { get; set; }
        public ConcurrentDictionary<string, int> ChatMessages { get; set; } = new();
    }
}

