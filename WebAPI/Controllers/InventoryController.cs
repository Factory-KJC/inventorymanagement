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
    /// <summary>
    /// 在庫管理APIのコントローラー
    /// </summary>
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

        /// <summary>
        /// 在庫一覧を取得するエンドポイント（GET /api/inventory）
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItems>>> GetInventoryItems()
        {
            return await _context.InventoryItems.ToListAsync();
        }


        /// <summary>
        /// 指定したIDの在庫を取得するエンドポイント（GET /api/inventory/{id}）
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 在庫を追加するエンドポイント（POST /api/inventory）
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<InventoryItems>> CreateInventoryItem(InventoryItems item)
        {
            _context.InventoryItems.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetInventoryItem), new { id = item.Id }, item);
        }

        /// <summary>
        /// 在庫を更新するエンドポイント（PUT /api/inventory/{id}）
        /// </summary>
        /// <param name="id"></param>
        /// <param name="item"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 在庫を削除するエンドポイント（DELETE /api/inventory/{id}）
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 在庫が存在するか確認するエンドポイント
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private bool InventoryItemExists(int id)
        {
            return _context.InventoryItems.Any(e => e.Id == id);
        }
    }
}
