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
    [Authorize]  // JWT�F�؂��K�v
    public class InventoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ?? �݌Ɉꗗ���擾�iGET /api/inventory�j
        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItems>>> GetInventoryItems()
        {
            return await _context.InventoryItems.ToListAsync();
        }

        // ?? �w�肵��ID�̍݌ɂ��擾�iGET /api/inventory/{id}�j
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

        // ?? �݌ɂ�ǉ��iPOST /api/inventory�j
        [HttpPost]
        public async Task<ActionResult<InventoryItems>> CreateInventoryItem(InventoryItems item)
        {
            _context.InventoryItems.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetInventoryItem), new { id = item.Id }, item);
        }

        // ?? �݌ɂ��X�V�iPUT /api/inventory/{id}�j
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

        // ?? �݌ɂ��폜�iDELETE /api/inventory/{id}�j
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

        // ?? �݌ɂ����݂��邩�m�F
        private bool InventoryItemExists(int id)
        {
            return _context.InventoryItems.Any(e => e.Id == id);
        }
    }
}
