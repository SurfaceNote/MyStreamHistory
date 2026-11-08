using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.Viewers;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Application.Services;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class GetRankingExclusionsConsumer(IRankingExclusionRepository repository) : IConsumer<GetRankingExclusionsRequest>
{
    public async Task Consume(ConsumeContext<GetRankingExclusionsRequest> context)
    {
        var accounts = await repository.ListAsync(context.CancellationToken);
        var names = await repository.GetDisplayNamesAsync(accounts.Select(a => a.Login).ToList(), context.CancellationToken);

        var candidates = context.Message.IncludeCandidates
            ? BotCandidateRules.Find(await repository.GetHighPresenceRowsAsync(
                BotCandidateRules.MinChannelObservedMinutes, BotCandidateRules.MinRowShare, context.CancellationToken))
            : [];

        await context.RespondAsync(new GetRankingExclusionsResponse
        {
            Accounts = accounts.Select(a => new RankingExcludedAccountDto
            {
                Login = a.Login,
                DisplayName = names.GetValueOrDefault(a.Login),
                AddedBy = a.AddedBy,
                AddedAt = a.AddedAt
            }).ToList(),
            Candidates = candidates
        });
    }
}

public class ChangeRankingExclusionConsumer(
    IRankingExclusionRepository repository,
    ILogger<ChangeRankingExclusionConsumer> logger) : IConsumer<ChangeRankingExclusionRequest>
{
    public async Task Consume(ConsumeContext<ChangeRankingExclusionRequest> context)
    {
        var message = context.Message;
        var login = RankingExclusions.NormalizeLogin(message.Login);
        if (login == null)
        {
            await context.RespondAsync(new ChangeRankingExclusionResponse { Success = false, Error = "invalid_login" });
            return;
        }

        var actor = string.IsNullOrWhiteSpace(message.Actor) ? "admin" : message.Actor.Trim();
        var changed = message.Exclude
            ? await repository.AddAsync(login, actor, context.CancellationToken)
            : await repository.RemoveAsync(login, context.CancellationToken);

        logger.LogInformation("Ranking exclusion {Action} for {Login} by {Actor}; changed: {Changed}",
            message.Exclude ? "add" : "remove", login, actor, changed);

        await context.RespondAsync(new ChangeRankingExclusionResponse
        {
            Success = true,
            Error = changed ? null : message.Exclude ? "already_excluded" : "not_found"
        });
    }
}
