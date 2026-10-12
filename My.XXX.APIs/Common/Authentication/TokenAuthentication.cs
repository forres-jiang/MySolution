using My.XXX.APIs.Common.Responses;
using My.XXX.APIs.Configurations;
using My.XXX.Application.Authentication.Ports;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace My.XXX.APIs.Common.Authentication;

public static class TokenAuthentication
{
    public static void Validate(JwtConfig config)
    {
        if (config == null || Encoding.UTF8.GetByteCount(config.Secret ?? "") < 32 ||
            string.IsNullOrWhiteSpace(config.Issuer) || string.IsNullOrWhiteSpace(config.Audience))
        {
            throw new InvalidOperationException("Configure JwtConfig Secret (at least 32 UTF-8 bytes), Issuer and Audience through a secret provider.");
        }
        if (config.ExpiryInMinutes <= 0 || config.RefreshExpiryInMinutes <= config.ExpiryInMinutes)
        {
            throw new InvalidOperationException("Refresh token lifetime must exceed the positive access token lifetime.");
        }
    }

    public static void Configure(JwtBearerOptions options, JwtConfig config, string purpose)
    {
        Validate(config);

        var signingKeys = new List<SecurityKey> { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Secret)) };
        if (!string.IsNullOrWhiteSpace(config.SecondarySecret))
        {
            signingKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.SecondarySecret)));
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKeys = signingKeys,
            ValidIssuer = config.Issuer,
            ValidAudience = purpose == "refresh" ? config.Audience + ":refresh" : config.Audience,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                if (context.Principal?.FindFirst("token_use")?.Value != purpose)
                {
                    context.Fail("Invalid token purpose.");
                    return;
                }
                var sessionId = context.Principal.FindFirst("sid")?.Value;
                if (string.IsNullOrEmpty(sessionId))
                {
                    context.Fail("Session required.");
                    return;
                }
                var services = context.HttpContext.RequestServices;
                var validation = await services.GetRequiredService<ISessionService>().ValidateAsync(sessionId,
                    context.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    purpose == "refresh" ? context.Principal.FindFirst("jti")?.Value ?? "" : null,
                    context.HttpContext.RequestAborted);
                if (validation.IsFailed)
                {
                    context.Fail("Session expired or revoked.");
                    return;
                }
                var active = validation.Value;
                // 授权始终以当前权限为准，包括管理员被移除的情况。
                var identity = (ClaimsIdentity)context.Principal!.Identity!;
                foreach (var claim in identity.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == ClaimTypes.UserData ||
                    c.Type == ClaimTypes.Name || c.Type == ClaimTypes.Email).ToList())
                {
                    identity.RemoveClaim(claim);
                }
                foreach (var role in active.User.Roles)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
                identity.AddClaim(new Claim(ClaimTypes.UserData, JsonConvert.SerializeObject(new UserClaimsData { RoleIds = new System.Collections.Generic.List<System.Guid>(active.User.RoleIds) })));
                identity.AddClaim(new Claim(ClaimTypes.Name, active.User.UserName ?? ""));
                identity.AddClaim(new Claim(ClaimTypes.Email, active.User.Email ?? ""));
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await context.Response.WriteErrorAsync(StatusCodes.Status401Unauthorized,
                    "Authentication required.", "Authentication.Required", context.HttpContext.RequestAborted);
            },
            OnForbidden = async context =>
            {
                await context.Response.WriteErrorAsync(StatusCodes.Status403Forbidden,
                    "Permission denied.", "Authorization.Forbidden", context.HttpContext.RequestAborted);
            }
        };
    }
}
