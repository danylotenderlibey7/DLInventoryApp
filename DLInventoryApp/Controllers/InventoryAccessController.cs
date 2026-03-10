using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.ViewModels.Inventories.Tabs.Access;
using DLInventoryApp.ViewModels.Inventories.Tabs.Access.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Controllers
{
    [Authorize]
    public class InventoryAccessController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public InventoryAccessController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost("Inventories/{inventoryId:guid}/Access/Add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAccess(Guid inventoryId, [FromBody] InventoryAccessAddDto dto)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Unauthorized();
            var isAdmin = User.IsInRole("Admin");
            var inv = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .Select(x => new { x.Id, x.OwnerId })
                .SingleOrDefaultAsync();
            if (inv == null) return NotFound();
            if (!isAdmin && inv.OwnerId != currentUserId) return NotFound();
            var email = (dto.Email ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { ok = false, error = "Email is required." });
            var user = await _context.Users
                .Where(u => u.Email != null && u.NormalizedEmail == email.ToUpper())
                .Select(u => new { u.Id, u.Email, u.UserName })
                .SingleOrDefaultAsync();
            if (user == null)
                return BadRequest(new { ok = false, error = "User with this email was not found." });
            if (user.Id == inv.OwnerId)
                return BadRequest(new { ok = false, error = "This user already has write access." });
            var exists = await _context.InventoryWriteAccesses
                .AnyAsync(x => x.InventoryId == inventoryId && x.UserId == user.Id);
            if (exists)
                return BadRequest(new { ok = false, error = "This user already has write access." });
            _context.InventoryWriteAccesses.Add(new InventoryWriteAccess
            {
                InventoryId = inventoryId,
                UserId = user.Id
            });
            await _context.SaveChangesAsync();
            return Ok(new { ok = true, user = new { userId = user.Id, email = user.Email, username = user.UserName } });
        }
        [HttpPost("Inventories/{inventoryId:guid}/Access/Remove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAccess(Guid inventoryId, [FromBody] InventoryAccessRemoveDto dto)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Unauthorized();
            var isAdmin = User.IsInRole("Admin");
            var inv = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .Select(x => new { x.Id, x.OwnerId })
                .SingleOrDefaultAsync();
            if (inv == null) return NotFound();
            if (!isAdmin && inv.OwnerId != currentUserId) return NotFound();
            var userId = (dto.UserId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest(new { ok = false, error = "UserId is required." });
            var access = await _context.InventoryWriteAccesses
                .SingleOrDefaultAsync(x => x.InventoryId == inventoryId && x.UserId == userId);
            if (access != null)
            {
                _context.InventoryWriteAccesses.Remove(access);
                await _context.SaveChangesAsync();
            }
            return Ok(new { ok = true });
        }
        [HttpPost("Inventories/{inventoryId:guid}/Access/SetPublic")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPublic(Guid inventoryId, [FromBody] InventoryAccessSetPublicDto dto)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Unauthorized();
            var isAdmin = User.IsInRole("Admin");
            var inventory = await _context.Inventories
                .Where(inv => inv.Id == inventoryId)
                .SingleOrDefaultAsync();
            if (inventory == null) return NotFound();
            if (!isAdmin && inventory.OwnerId != currentUserId) return NotFound();
            inventory.IsPublic = dto.IsPublic;
            await _context.SaveChangesAsync();
            return Ok(new { ok = true, isPublic = inventory.IsPublic });
        }
        [HttpGet("Inventories/{inventoryId:guid}/Access/SearchUsers")]
        public async Task<IActionResult> SearchUsers(Guid inventoryId, string q)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Unauthorized();
            var isAdmin = User.IsInRole("Admin");
            var inventory = await _context.Inventories
               .Where(x => x.Id == inventoryId)
               .Select(x => new { x.Id, x.OwnerId })
               .SingleOrDefaultAsync();
            if (inventory == null) return NotFound();
            if (!isAdmin && inventory.OwnerId != currentUserId) return NotFound();
            var term = (q ?? string.Empty).Trim();
            if (term.Length < 2)  return Ok(new List<AccessSearchUserVm>());
            var normalized = term.ToUpperInvariant();
            var existingUserIds = await _context.InventoryWriteAccesses
                .Where(x => x.InventoryId == inventoryId)
                .Select(x => x.UserId)
                .ToListAsync();
            var users = await _context.Users
                .Where(u =>
                    (u.NormalizedEmail != null && u.NormalizedEmail.Contains(normalized)) ||
                    (u.NormalizedUserName != null && u.NormalizedUserName.Contains(normalized)))
                .Where(u => u.Id != inventory.OwnerId)
                .Where(u => !existingUserIds.Contains(u.Id))
                .OrderBy(u => u.UserName ?? u.Email)
                .Select(u => new AccessSearchUserVm
                {
                    UserId = u.Id,
                    UserName = u.UserName,
                    Email = u.Email!
                })
                .Take(8)
                .ToListAsync();
            return Ok(users);
        }
    }
}