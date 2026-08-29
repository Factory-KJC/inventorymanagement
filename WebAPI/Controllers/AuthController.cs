using InventoryAPI.Application.Auth;
using InventoryAPI.Contracts.Auth;
using InventoryAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace InventoryAPI.Controllers;

/// <summary>
/// 認証コントローラー
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    ApplicationDbContext db,
    JwtTokenService tokenService,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// ユーザー名とパスワードを検証し、2時間有効なアクセストークンを発行します。
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Username == username, cancellationToken);

        // ユーザーの存在有無を応答から推測できないよう、失敗理由は統一します。
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new MessageResponse("ユーザー名またはパスワードが違います。"));

        var response = tokenService.CreateTokenPair(user);
        db.RefreshTokens.Add(tokenService.CreateRefreshToken(user.Id, response.RefreshToken));
        await db.SaveChangesAsync(cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// 有効な更新トークンを一度だけ使用し、新しいアクセストークンと更新トークンへ交換します。
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var hash = JwtTokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await db.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (storedToken is null || storedToken.RevokedAt is not null || storedToken.ExpiresAt <= now)
            return Unauthorized(new MessageResponse("更新トークンが無効または期限切れです。"));

        var response = tokenService.CreateTokenPair(storedToken.User);
        var replacement = tokenService.CreateRefreshToken(storedToken.UserId, response.RefreshToken);
        var updated = await db.RefreshTokens
            .Where(token => token.Id == storedToken.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.ReplacedByTokenId, replacement.Id),
                cancellationToken);
        if (updated != 1)
            return Unauthorized(new MessageResponse("更新トークンは既に使用されています。"));

        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(response);
    }
}
