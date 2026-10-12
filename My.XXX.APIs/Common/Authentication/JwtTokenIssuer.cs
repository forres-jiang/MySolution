using My.XXX.APIs.Configurations;
using My.XXX.Application.Authentication.Models;
using My.XXX.Application.Authentication.Ports;
using My.XXX.Domain.Authentication.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace My.XXX.APIs.Common.Authentication;

public sealed class JwtTokenIssuer(IOptions<JwtConfig> options, TimeProvider clock) : ITokenIssuer
{
    public SessionTokens Create(UserIdentity user, string sessionId, string refreshTokenId, DateTime refreshExpiration)
    {
        var config = options.Value;
        var accessExpiration = clock.GetUtcNow().UtcDateTime.AddMinutes(config.ExpiryInMinutes);
        if (accessExpiration > refreshExpiration)
        {
            accessExpiration = refreshExpiration;
        }

        string Build(string purpose, string id, DateTime expiration)
        {
            var claims = new List<Claim>
            {
                new("sid", sessionId),
                new("jti", id),
                new("token_use", purpose),
                new(ClaimTypes.NameIdentifier, user.UserId),
                new(ClaimTypes.Name, user.UserName ?? ""),
                new(ClaimTypes.Email, user.Email ?? ""),
                new(ClaimTypes.UserData, JsonConvert.SerializeObject(new UserClaimsData { RoleIds = new List<Guid>(user.RoleIds) }))
            };

            foreach (var role in user.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(config.Issuer, config.Audience + (purpose == "refresh" ? ":refresh" : ""),
                claims, expires: expiration, signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Secret)), SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        return new SessionTokens(
            Build("access", Guid.NewGuid().ToString("N"), accessExpiration),
            Build("refresh", refreshTokenId, refreshExpiration),
            (int)Math.Ceiling((accessExpiration - clock.GetUtcNow().UtcDateTime).TotalMinutes));
    }
}