using InventoryAPI.Contracts.Auth;
using InventoryAPI.Data;
using InventoryAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

/// <summary>
/// システムを最初に利用する管理ユーザーを登録します。
/// </summary>
[ApiController]
[Route("api/users")]
public sealed class UserController(ApplicationDbContext db, IConfiguration configuration) : ControllerBase
{
    private const int MinimumUsernameLength = 3;
    private const int MinimumPasswordLength = 12;

    /// <summary>
    /// ユーザーがまだ存在しない場合に限り、初回管理ユーザーを登録します。
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<MessageResponse>> Register(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsSetupRequestAllowed())
            return NotFound();

        var username = request.Username.Trim();
        if (username.Length < MinimumUsernameLength || request.Password.Length < MinimumPasswordLength)
            return BadRequest(new MessageResponse("ユーザー名は3文字以上、パスワードは12文字以上にしてください。"));

        // 現段階では単一管理ユーザーのみを許可し、意図しない公開登録を防ぎます。
        if (await db.Users.AnyAsync(cancellationToken))
            return Conflict(new MessageResponse("初回ユーザーは登録済みです。追加メンバー機能は現在未実装です。"));

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new MessageResponse("ユーザー登録に成功しました。"));
    }

    private bool IsSetupRequestAllowed()
    {
        if (HttpContext.Connection.RemoteIpAddress?.IsLoopback() == true)
            return true;

        var configuredToken = configuration["Setup:Token"];
        var suppliedToken = Request.Headers["X-Setup-Token"].ToString();
        if (string.IsNullOrEmpty(configuredToken) || string.IsNullOrEmpty(suppliedToken))
            return false;

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(configuredToken),
            System.Text.Encoding.UTF8.GetBytes(suppliedToken));
    }
}

internal static class IpAddressExtensions
{
    public static bool IsLoopback(this System.Net.IPAddress address) => System.Net.IPAddress.IsLoopback(address);
}
