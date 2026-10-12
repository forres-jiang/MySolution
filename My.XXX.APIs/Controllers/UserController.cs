using My.XXX.APIs.Common.Mapping;
using My.XXX.APIs.Common.Responses;
using My.XXX.APIs.Contracts.Authentication;
using My.XXX.APIs.Contracts.Shared;
using My.XXX.Application.Authentication.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.Tasks;

namespace My.XXX.APIs.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UserController(IAuthenticationService authentication, IUserService users) : ControllerBase
{
    [HttpPost("RefreshToken")]
    [EnableRateLimiting("refresh")]
    [Authorize(AuthenticationSchemes = "Refresh")]
    public async Task<ActionResult<APIResponse<TokenResponse>>> RefreshToken()
    {
        var result = await authentication.RefreshAsync(HttpContext.RequestAborted);
        return result.ToMappedHttpResult(renewal => new TokenResponse(
            renewal.Tokens.AccessToken, renewal.Tokens.RefreshToken, renewal.Tokens.ExpiryInMinutes),
            HttpContext.TraceIdentifier);
    }

    [HttpDelete("Session")]
    public async Task<NoContentResult> RevokeSession()
    {
        await authentication.LogoutAsync(HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpGet("GetRoles")]
    public APIResponse<UserInfoResponse> GetRoles() =>
        new(1, "Success", WireMapper.Instance.ToUserInfoResponse(users.CurrentUser), traceId: HttpContext.TraceIdentifier);
}
