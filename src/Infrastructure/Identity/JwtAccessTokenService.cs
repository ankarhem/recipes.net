using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using App.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Identity;

public sealed class JwtAccessTokenService(JwtAccessTokenOptions options) : IAccessTokenService
{
    public AccessToken Generate(Guid userId, string email)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(options.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("email", email),
            new Claim("jti", Guid.NewGuid().ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials
        );

        var handler = new JwtSecurityTokenHandler();
        return new AccessToken { Token = handler.WriteToken(token), ExpiresAt = expires };
    }
}
