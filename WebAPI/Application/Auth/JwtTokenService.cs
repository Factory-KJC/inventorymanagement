using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using InventoryAPI.Configuration;
using InventoryAPI.Contracts.Auth;
using InventoryAPI.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InventoryAPI.Application.Auth;

/// <summary>
/// 認証済みユーザーに対するJWTの発行を担当します。
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private readonly JwtOptions jwtOptions = options.Value;

    public LoginResponse CreateTokenPair(User user)
    {
        var issuedAt = timeProvider.GetUtcNow();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(ClaimTypes.Name, user.Username)]),
            IssuedAt = issuedAt.UtcDateTime,
            Expires = issuedAt.Add(AccessTokenLifetime).UtcDateTime,
            Issuer = jwtOptions.Issuer,
            Audience = jwtOptions.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var handler = new JwtSecurityTokenHandler();
        var accessToken = handler.WriteToken(handler.CreateToken(descriptor));
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return new LoginResponse(accessToken, refreshToken, issuedAt.Add(AccessTokenLifetime));
    }

    public RefreshToken CreateRefreshToken(int userId, string token)
    {
        var now = timeProvider.GetUtcNow();
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashRefreshToken(token),
            CreatedAt = now,
            ExpiresAt = now.Add(RefreshTokenLifetime)
        };
    }

    public static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
