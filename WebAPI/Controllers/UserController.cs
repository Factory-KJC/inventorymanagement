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
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            if (_context.Users.Any(u => u.Username == request.Username))
                return BadRequest(new { message = "❌ このユーザー名は既に使用されています" });

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var user = new User { Username = request.Username, Password_Hash = hashedPassword };
            _context.Users.Add(user);
            _context.SaveChanges();

            return Ok(new { message = "✅ ユーザー登録成功" });
        }
    }
    public class RegisterRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
