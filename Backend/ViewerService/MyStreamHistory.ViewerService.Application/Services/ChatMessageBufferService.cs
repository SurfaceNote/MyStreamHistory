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

    public bool IsStreamActive(string twitchUserId)
    {
        return _streamBuffers.ContainsKey(twitchUserId);
    }

    public bool IsStreamActive(string twitchUserId, Guid streamSessionId)
    {
        return _streamBuffers.TryGetValue(twitchUserId, out var buffer)
            && buffer.StreamSessionId == streamSessionId;
    }

    public bool IsAccrualEnabled(string twitchUserId, Guid streamSessionId)
    {
        return _streamBuffers.TryGetValue(twitchUserId, out var buffer)
            && buffer.StreamSessionId == streamSessionId
            && !buffer.IsPaused;
    }

    public int GetActiveStreamCount()
    {
        return _streamBuffers.Count;
    }

    public void AddChatMessage(string twitchUserId, string chatterUserId, int characterCount)
    {
        if (!_streamBuffers.TryGetValue(twitchUserId, out var buffer) || buffer.IsPaused)
            return;

        buffer.ChatMessages.AddOrUpdate(chatterUserId, characterCount, (_, existing) => existing + characterCount);
    }

    public bool UpdateStreamCategory(string twitchUserId, Guid streamSessionId, Guid newCategoryId)
    {
        if (_streamBuffers.TryGetValue(twitchUserId, out var buffer)
            && buffer.StreamSessionId == streamSessionId)
        {
            buffer.CurrentCategoryId = newCategoryId;
            return true;
        }

        return false;
    }

    public void PauseStream(string twitchUserId, Guid streamSessionId)
    {
        if (_streamBuffers.TryGetValue(twitchUserId, out var buffer)
            && buffer.StreamSessionId == streamSessionId
            && !buffer.IsPaused)
        {
            buffer.IsPaused = true;
            buffer.ChatMessages.Clear();
        }
    }

    public void ResumeStream(string twitchUserId, Guid streamSessionId)
    {
        if (_streamBuffers.TryGetValue(twitchUserId, out var buffer)
            && buffer.StreamSessionId == streamSessionId
            && buffer.IsPaused)
        {
            buffer.ChatMessages.Clear();
            buffer.IsPaused = false;
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
            if (buffer.IsPaused)
            {
                buffer.ChatMessages.Clear();
                continue;
            }

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

    public bool RemoveStream(string twitchUserId, Guid streamSessionId)
    {
        if (!_streamBuffers.TryGetValue(twitchUserId, out var buffer)
            || buffer.StreamSessionId != streamSessionId)
        {
            return false;
        }

        return ((ICollection<KeyValuePair<string, StreamBuffer>>)_streamBuffers)
            .Remove(new KeyValuePair<string, StreamBuffer>(twitchUserId, buffer));
    }

    private class StreamBuffer
    {
        public Guid StreamSessionId { get; set; }
        public Guid? CurrentCategoryId { get; set; }
        public bool IsPaused { get; set; }
        public ConcurrentDictionary<string, int> ChatMessages { get; set; } = new();
    }
}

