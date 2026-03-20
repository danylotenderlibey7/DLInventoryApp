using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Salesforce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DLInventoryApp.Controllers
{
    [Authorize]
    public class SalesforceController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISalesforceService _salesforceService;
        public SalesforceController(UserManager<ApplicationUser> userManager, ISalesforceService salesforceService)
        {
            _userManager = userManager;
            _salesforceService = salesforceService;
        }
        [HttpGet]
        public async Task<IActionResult> ExportForm()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            return View(new SalesforceExportVm { Email = user.Email ?? "" });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportForm(SalesforceExportVm vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            try
            {
                await _salesforceService.ExportContactAsync(
                    vm.FirstName, vm.LastName, vm.Email, vm.Phone, vm.CompanyName, vm.JobTitle ?? "");
                TempData["StatusMessage"] = "Successfully exported to Salesforce!";
                return RedirectToAction(nameof(ExportForm));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }
    }
}