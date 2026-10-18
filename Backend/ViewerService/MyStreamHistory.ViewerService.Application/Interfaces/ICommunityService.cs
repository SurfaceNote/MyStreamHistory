using MyStreamHistory.Shared.Base.Contracts.Community;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;

namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface ICommunityService
{
    Task RecordPollAsync(string channel, Guid session, DateTime observedAt, IReadOnlyCollection<TwitchChatterDto>? chatters, CancellationToken ct);
    Task RecordMessageAsync(ChatMessageEventContract message, Guid session, CancellationToken ct);
    Task<int> DeleteExpiredMessageReceiptsAsync(CancellationToken ct);
    Task<CommunityResponse> HandleAsync(CommunityRequest request, CancellationToken ct);
    Task RecalculatePendingAsync(CancellationToken ct);
}
