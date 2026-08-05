using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InventoryAPI.Configuration;
using InventoryAPI.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InventoryAPI.Application.Auth;

/// <summary>
/// 認証済みユーザーに対するJWTの発行を担当します。
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(2);
    private readonly JwtOptions jwtOptions = options.Value;

    public string CreateToken(User user)
    {
        var issuedAt = timeProvider.GetUtcNow();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(ClaimTypes.Name, user.Username)]),
            IssuedAt = issuedAt.UtcDateTime,
            Expires = issuedAt.Add(TokenLifetime).UtcDateTime,
            Issuer = jwtOptions.Issuer,
            Audience = jwtOptions.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
