using DLInventoryApp.Models;
using DLInventoryApp.Services;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DLInventoryApp.Controllers
{
    [Authorize]
    public class SupportController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISupportService _supportService;
        public SupportController(UserManager<ApplicationUser> userManager, ISupportService supportService)
        {
            _userManager = userManager;
            _supportService = supportService;
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTicket(SupportTicketVm vm)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            if (!ModelState.IsValid) return BadRequest(new { ok = false, error = "Invalid data." });
            try
            {
                var ticketId = await _supportService.CreateTicketAsync(vm, userId);
                return Ok(new { ok = true, ticketId });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { ok = false, error = ex.Message });
            }
        }
    }
}