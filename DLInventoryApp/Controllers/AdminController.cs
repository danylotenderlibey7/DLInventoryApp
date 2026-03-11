using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Admin;
using DLInventoryApp.ViewModels.Common.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ISearchService _search;
        const string AdminRole = "Admin";
        public AdminController(UserManager<ApplicationUser> userManager, 
            ApplicationDbContext context, ISearchService search)
        {
            _userManager = userManager;
            _context = context;
            _search = search;
        }
        public async Task<IActionResult> Users(int page = 1, int pageSize = 6)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 50 ? 6 : pageSize;
            var adminRoleIdQuery = _context.Roles
                .Where(r => r.Name == AdminRole)
                .Select(r => r.Id);
            var totalCount = await _context.Users.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages > 0 && page > totalPages) page = totalPages;
            var skip = (page - 1) * pageSize;
            var listVm = await _context.Users
                .Select(u => new UserAdminVm
                {
                    Id = u.Id,
                    UserName = u.UserName ?? "User",
                    Email = u.Email!,
                    EmailConfirmed = u.EmailConfirmed,
                    IsBlocked = u.IsBlocked,
                    IsAdmin = _context.UserRoles.Any(ur => ur.UserId == u.Id && adminRoleIdQuery.Contains(ur.RoleId))
                })
                .OrderBy(u => u.Email)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();
            var vm = new PagedVm<UserAdminVm>
            {
                Items = listVm,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
            return View(vm);
        }
        [HttpPost("Block")]
        public async Task<IActionResult> Block(List<string> userIds)
        {
            if (userIds == null || userIds.Count == 0) return RedirectToAction("Users");
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
            foreach(var user in users)
            {
                user.IsBlocked = true;
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
            if (userIds == null || userIds.Count == 0) return RedirectToAction("Users");
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
            foreach (var user in users)
            {
                await _userManager.DeleteAsync(user);
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
                var isAdmin = await _userManager.IsInRoleAsync(user, AdminRole);
                if (!isAdmin)
                {
                    await _userManager.AddToRoleAsync(user, AdminRole);
                }
            }
            return RedirectToAction("Users");
        }
        [HttpPost("RemoveAdmin")]
        public async Task<IActionResult> RemoveAdmin(List<string> userIds)
        {
            if (userIds == null || userIds.Count == 0) return RedirectToAction("Users");
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
            foreach (var user in users)
            {
                var isAdmin = await _userManager.IsInRoleAsync(user, AdminRole);
                if (isAdmin)
                {
                    await _userManager.RemoveFromRoleAsync(user, AdminRole);
                }
            }
            return RedirectToAction("Users");
        }
        
    }
}
