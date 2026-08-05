using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using InventoryAPI.Data;
using InventoryAPI.Models;
using BCrypt.Net;
using System;

namespace InventoryAPI.Controllers
{
    /// <summary>
    /// ユーザー管理APIのコントローラー
    /// </summary>
    [Route("api/users")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// ユーザー登録エンドポイント
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
        {
            var username = request.Username.Trim();
            if (username.Length < 3 || request.Password.Length < 12)
                return BadRequest(new { message = "ユーザー名は3文字以上、パスワードは12文字以上にしてください。" });
            if (await _context.Users.AnyAsync(cancellationToken))
                return Conflict(new { message = "初回ユーザーは登録済みです。追加メンバー機能は現在未実装です。" });

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var user = new User { Username = username, Password_Hash = hashedPassword };
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new { message = "ユーザー登録に成功しました。" });
        }
    }

    /// <summary>
    /// ユーザー登録リクエストのモデル
    /// </summary>
    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
