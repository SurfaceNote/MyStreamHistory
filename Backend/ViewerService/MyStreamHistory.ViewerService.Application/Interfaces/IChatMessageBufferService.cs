using MyStreamHistory.ViewerService.Application.DTOs;

namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IChatMessageBufferService
{
    void InitializeStream(string twitchUserId, Guid streamSessionId, Guid? currentCategoryId);
    bool IsStreamActive(string twitchUserId);
    int GetActiveStreamCount();
    void AddChatMessage(string twitchUserId, string chatterUserId, int characterCount);
    void UpdateStreamCategory(string twitchUserId, Guid newCategoryId);
    DataCollectionSnapshot CreateSnapshot();
    void RemoveStream(string twitchUserId);
}

