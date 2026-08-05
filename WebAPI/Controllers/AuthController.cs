using InventoryAPI.Application.Auth;
using InventoryAPI.Contracts.Auth;
using InventoryAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

/// <summary>
/// 認証コントローラー
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(ApplicationDbContext db, JwtTokenService tokenService) : ControllerBase
{
    /// <summary>
    /// ユーザー名とパスワードを検証し、2時間有効なアクセストークンを発行します。
    /// </summary>
    [HttpPost("login")]
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

        return Ok(new LoginResponse(tokenService.CreateToken(user)));
    }
}
