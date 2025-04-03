using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using InventoryAPI.Data;
using InventoryAPI.Models;
using BCrypt.Net;
using System;

namespace InventoryAPI.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 🔹 ユーザー登録エンドポイント
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            if (await _context.Users.AnyAsync(u => u.Username == user.Username))
            {
                return BadRequest("❌ ユーザー名は既に使用されています。");
            }

            user.Password_Hash = BCrypt.Net.BCrypt.HashPassword(user.Password_Hash); // 再ハッシュ（念のため）

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok("✅ ユーザー登録成功！");
        }
    }
}
