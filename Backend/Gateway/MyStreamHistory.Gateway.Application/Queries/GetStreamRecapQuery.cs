using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;

namespace MyStreamHistory.Gateway.Application.Queries;

/// <summary>
/// The recap names individual chat accounts, so only the streamer and the admin panel can read it.
/// </summary>
public class GetStreamRecapQuery : IRequest<StreamRecapDto>
{
    public Guid StreamSessionId { get; set; }

    /// <summary>The signed-in user. Must be the stream's broadcaster unless <see cref="AdminAccess"/> is set.</summary>
    public int? RequesterTwitchUserId { get; set; }

    /// <summary>Set only by the admin endpoint, which is protected by the admin service policy.</summary>
    public bool AdminAccess { get; set; }

    public static bool CanRead(GetStreamRecapQuery query, int broadcasterTwitchUserId) =>
        query.AdminAccess || query.RequesterTwitchUserId == broadcasterTwitchUserId;
}
