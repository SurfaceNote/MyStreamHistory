using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MyStreamHistory.Shared.Api.Authorization;

namespace MyStreamHistory.Shared.Api.Attributes;

public class AllowExpiredAuthorizeAttribute : AuthorizeAttribute, IAuthorizationFilter
{
    public AllowExpiredAuthorizeAttribute()
    {
        Policy = PolicyNames.AllowExpiredJwt;
    }
    
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var config = context.HttpContext.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;

        var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config!["Jwt:Key"]!));

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            context.Result = new UnauthorizedResult();
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                ValidIssuer = config["Jwt:Issuer"],
                ValidAudience = config["Jwt:Audience"],
                IssuerSigningKey = key
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            context.HttpContext.User = principal;

            if (!(validatedToken is JwtSecurityToken))
            {
                context.Result = new UnauthorizedResult();
            }
        }
        catch (Exception)
        {
            context.Result = new UnauthorizedResult();
        }
    }
}