using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Prng;

namespace DLInventoryApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ISearchService _search;
        public AdminController(UserManager<ApplicationUser> userManager, 
            ApplicationDbContext context, ISearchService search)
        {
            _userManager = userManager;
            _context = context;
            _search = search;
        }
        public async Task<IActionResult> Users()
        {
            const string adminRoleName = "Admin";
            var adminRoleIdQuery = _context.Roles
                .Where(r => r.Name == adminRoleName)
                .Select(r => r.Id);
            var listVm = await _context.Users
                .Select(u => new UserAdminVm
                {
                    Id = u.Id,
                    Email = u.Email!,
                    EmailConfirmed = u.EmailConfirmed,
                    IsBlocked = u.IsBlocked,
                    IsAdmin = _context.UserRoles.Any(ur => ur.UserId == u.Id && adminRoleIdQuery.Contains(ur.RoleId))
                }).ToListAsync();
            return View(listVm);
        }
        [HttpPost("Block")]
        public async Task<IActionResult> Block(List<string> userIds)
        {
            const string adminRoleName = "Admin";
            var adminRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.Name == adminRoleName);
            if (adminRole == null) return RedirectToAction("Users");
            var adminsCount = await _context.UserRoles.CountAsync(ur => ur.RoleId == adminRole.Id);
            if (userIds == null || userIds.Count == 0) return RedirectToAction("Users");
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
            foreach(var user in users)
            {
                var isAdmin = await _userManager.IsInRoleAsync(user, adminRoleName);
                if (isAdmin && adminsCount <= 1) continue;
                user.IsBlocked = true;
                if (isAdmin) adminsCount--;
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Users");
        }
        [HttpPost("Unblock")]
        public async Task<IActionResult> Unblock(List<string> userIds)
        {
            if (userIds == null || userIds.Count == 0) return RedirectToAction("Users");
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
            foreach (var user in users)
            {
                user.IsBlocked = false;
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Users");
        }
        [HttpPost("Delete")]
        public async Task<IActionResult> Delete(List<string> userIds)
        {
            const string adminRoleName = "Admin";
            var adminRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.Name == adminRoleName);
            if (adminRole == null) return RedirectToAction("Users");
            var adminsCount = await _context.UserRoles.CountAsync(ur => ur.RoleId == adminRole.Id);
            if (userIds == null || userIds.Count == 0) return RedirectToAction("Users");
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
            foreach (var user in users)
            {
                var isAdmin = await _userManager.IsInRoleAsync(user, adminRoleName);
                if (isAdmin && adminsCount <= 1) continue;
                await _userManager.DeleteAsync(user);
                if (isAdmin) adminsCount--;
            }
            return RedirectToAction("Users");
        }
        [HttpPost("AddAdmin")]
        public async Task<IActionResult> AddAdmin(List<string> userIds)
        {
            if (userIds == null || userIds.Count == 0) return RedirectToAction("Users");
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
            foreach (var user in users)
            {
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                if (!isAdmin)
                {
                    await _userManager.AddToRoleAsync(user, "Admin");
                }
            }
            return RedirectToAction("Users");
        }
        [HttpPost("RemoveAdmin")]
        public async Task<IActionResult> RemoveAdmin(List<string> userIds)
        {
            const string adminRoleName = "Admin";
            var adminRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.Name == adminRoleName);
            if (adminRole == null) return RedirectToAction("Users");
            var adminsCount = await _context.UserRoles.CountAsync(ur => ur.RoleId == adminRole.Id);
            if (userIds == null || userIds.Count == 0) return RedirectToAction("Users");
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
            foreach (var user in users)
            {
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                if (isAdmin && adminsCount <= 1) continue;
                if (isAdmin)
                {
                    var result = await _userManager.RemoveFromRoleAsync(user, adminRoleName);
                    if (result.Succeeded) adminsCount--;
                }
            }
            return RedirectToAction("Users");
        }
        
    }
}
