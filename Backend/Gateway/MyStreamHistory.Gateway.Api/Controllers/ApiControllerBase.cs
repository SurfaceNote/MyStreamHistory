using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MyStreamHistory.Gateway.Api.Controllers
{
    public class ApiControllerBase : ControllerBase
    {
        protected Guid UserId
        {
            get
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new ArgumentNullException(nameof(UserId), "UserId cannot be null");

                if (Guid.TryParse(userId, out var userIdGuid))
                {
                    return userIdGuid;
                }
                throw new ArgumentException(nameof(UserId), "UserId must be Guid");
            }
        }

        protected int TwitchUserId
        {
            get
            {
                var twitchUserId = User.FindFirstValue("TwitchId") ?? throw new ArgumentNullException(nameof(TwitchUserId), "TwitchUserId cannot be null");

                if (int.TryParse(twitchUserId, out var parsedTwitchUserId))
                {
                    return parsedTwitchUserId;
                }

                throw new ArgumentException(nameof(TwitchUserId), "TwitchUserId must be int");
            }
        }
    }
}
