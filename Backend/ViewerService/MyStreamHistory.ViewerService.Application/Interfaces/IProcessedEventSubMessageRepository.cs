using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IProcessedEventSubMessageRepository
{
    Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default);
    Task AddAsync(ProcessedEventSubMessage message, CancellationToken cancellationToken = default);
    Task DeleteOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default);
}

