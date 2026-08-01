using MyStreamHistory.ViewerService.Application.DTOs;

namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IChatMessageBufferService
{
    void InitializeStream(string twitchUserId, Guid streamSessionId, Guid? currentCategoryId);
    bool IsStreamActive(string twitchUserId);
    bool IsStreamActive(string twitchUserId, Guid streamSessionId);
    bool IsAccrualEnabled(string twitchUserId, Guid streamSessionId);
    int GetActiveStreamCount();
    void AddChatMessage(string twitchUserId, string chatterUserId, int characterCount);
    bool UpdateStreamCategory(string twitchUserId, Guid streamSessionId, Guid newCategoryId);
    void PauseStream(string twitchUserId, Guid streamSessionId);
    void ResumeStream(string twitchUserId, Guid streamSessionId);
    DataCollectionSnapshot CreateSnapshot();
    bool RemoveStream(string twitchUserId, Guid streamSessionId);
}

