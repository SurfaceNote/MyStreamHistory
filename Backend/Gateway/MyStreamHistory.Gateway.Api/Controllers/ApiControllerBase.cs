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
    }
}
