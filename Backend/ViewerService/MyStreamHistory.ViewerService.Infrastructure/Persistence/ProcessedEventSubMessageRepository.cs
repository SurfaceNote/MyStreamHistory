using Microsoft.EntityFrameworkCore;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Infrastructure.Persistence;

public class ProcessedEventSubMessageRepository : IProcessedEventSubMessageRepository
{
    private readonly ViewerServiceDbContext _context;

    public ProcessedEventSubMessageRepository(ViewerServiceDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default)
    {
        return await _context.ProcessedEventSubMessages
            .AnyAsync(m => m.MessageId == messageId, cancellationToken);
    }

    public async Task AddAsync(ProcessedEventSubMessage message, CancellationToken cancellationToken = default)
    {
        _context.ProcessedEventSubMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default)
    {
        var oldMessages = await _context.ProcessedEventSubMessages
            .Where(m => m.ProcessedAt < threshold)
            .ToListAsync(cancellationToken);

        _context.ProcessedEventSubMessages.RemoveRange(oldMessages);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

