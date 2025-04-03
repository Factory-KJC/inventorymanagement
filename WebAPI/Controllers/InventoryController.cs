using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryAPI.Data;
using InventoryAPI.Models;
using System;

namespace InventoryAPI.Controllers
{
    [Route("api/inventory")]
    [ApiController]
    [Authorize]  // JWT認証が必要
    public class InventoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ?? 在庫一覧を取得（GET /api/inventory）
        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItems>>> GetInventoryItems()
        {
            return await _context.InventoryItems.ToListAsync();
        }

        // ?? 指定したIDの在庫を取得（GET /api/inventory/{id}）
        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryItems>> GetInventoryItem(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            return item;
        }

        // ?? 在庫を追加（POST /api/inventory）
        [HttpPost]
        public async Task<ActionResult<InventoryItems>> CreateInventoryItem(InventoryItems item)
        {
            _context.InventoryItems.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetInventoryItem), new { id = item.Id }, item);
        }

        // ?? 在庫を更新（PUT /api/inventory/{id}）
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateInventoryItem(int id, InventoryItems item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }

            _context.Entry(item).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InventoryItemExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // ?? 在庫を削除（DELETE /api/inventory/{id}）
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInventoryItem(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            _context.InventoryItems.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ?? 在庫が存在するか確認
        private bool InventoryItemExists(int id)
        {
            return _context.InventoryItems.Any(e => e.Id == id);
        }
    }
}
