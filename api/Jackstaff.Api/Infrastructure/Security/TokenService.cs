using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Jackstaff.Api.Modules.Auth;
using Microsoft.IdentityModel.Tokens;

namespace Jackstaff.Api.Infrastructure.Security;

public sealed class TokenService(IConfiguration configuration)
{
    public TokenEnvelope Create(AuthUser user, TenantMembership tenant)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "Jackstaff";
        var audience = configuration["Jwt:Audience"] ?? "Jackstaff.Web";
        var key = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("JWT signing key is missing.");
        var minutes = int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var configuredMinutes)
            ? Math.Clamp(configuredMinutes, 5, 1440)
            : 240;
        var expires = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("name", user.Name),
            new("email", user.Email),
            new("tenant_id", tenant.TenantId.ToString()),
            new("tenant_slug", tenant.Slug),
            new("role", tenant.Role)
        };
        claims.AddRange(
            tenant.Permissions
                .Cast<string>()
                .Select(permission => new Claim("permission", permission))
        );

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: expires, signingCredentials: credentials);
        return new TokenEnvelope(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
