using My.XXX.Application.Authentication.Ports;
using My.XXX.Domain.Authentication.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace My.XXX.APIs.Common.Authentication;

public sealed class HttpCurrentRequest : IAuthenticationSession
{
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<HttpCurrentRequest> _logger;

    public HttpCurrentRequest(IHttpContextAccessor accessor, ILogger<HttpCurrentRequest> logger)
    {
        _accessor = accessor;
        _logger = logger;
    }

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;
    public string? SessionId => Principal.FindFirstValue("sid");
    public string? TokenId => Principal.FindFirstValue("jti");

    private ClaimsPrincipal Principal => _accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public UserIdentity? User
    {
        get
        {
            var principal = Principal;
            if (principal.Identity?.IsAuthenticated != true) return null;
            var roleIds = new List<Guid>();
            var userData = principal.FindFirstValue(ClaimTypes.UserData);
            if (!string.IsNullOrWhiteSpace(userData))
            {
                try { roleIds = JsonConvert.DeserializeObject<UserClaimsData>(userData)?.RoleIds ?? new List<Guid>(); }
                catch (JsonException ex) { _logger.LogWarning(ex, "Invalid user-data claim."); }
            }
            return new UserIdentity
            {
                UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!,
                UserName = principal.FindFirstValue(ClaimTypes.Name)!,
                Email = principal.FindFirstValue(ClaimTypes.Email)!,
                Roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToList(),
                RoleIds = roleIds
            };
        }
    }

    public DateTime TokenExpirationTime =>
        long.TryParse(Principal.FindFirstValue("exp"), out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime
            : DateTime.MinValue;
}
