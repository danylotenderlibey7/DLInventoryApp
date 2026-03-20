using System.Text.Json;
using DLInventoryApp.Data;
using DLInventoryApp.Dtos.Support;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Services
{
    public class SupportService : ISupportService
    {
        private static readonly JsonSerializerOptions _json = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOneDriveService _oneDrive;
        public SupportService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IOneDriveService oneDrive)
        {
            _context = context;
            _userManager = userManager;
            _oneDrive = oneDrive;
        }
        public async Task<string> CreateTicketAsync(SupportTicketVm vm, string userId)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");
            string? inventoryTitle = null;
            if (vm.InventoryId.HasValue)
            {
                inventoryTitle = await _context.Inventories
                    .Where(i => i.Id == vm.InventoryId.Value)
                    .Select(i => i.Title)
                    .SingleOrDefaultAsync();
            }
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var adminEmails = adminUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => u.Email!)
                .ToList();
            var ticketId = $"TKT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
            var payload = new SupportTicketPayload
            {
                TicketId = ticketId,
                ReportedBy = user.UserName ?? user.Email ?? "Unknown",
                ReportedByEmail = user.Email,
                InventoryTitle = inventoryTitle,
                Link = vm.CurrentUrl ?? "N/A",
                Priority = vm.Priority,
                Summary = vm.Summary,
                AdminEmails = adminEmails,
                CreatedAtUtc = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(payload, _json);
            await _oneDrive.UploadJsonAsync($"{ticketId}.json", json);
            return ticketId;
        }
    }
}