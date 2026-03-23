using DLInventoryApp.Data;
using DLInventoryApp.ViewModels.Inventories.Tabs.Odoo;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Services.Tabs
{
    public class OdooTabBuilder
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public OdooTabBuilder(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }
        public async Task<OdooTabVm> BuildAsync(Guid inventoryId, string title, bool canManage)
        {
            var inventory = await _context.Inventories
                .AsNoTracking()
                .Where(i => i.Id == inventoryId)
                .Select(i => new { i.ApiToken })
                .SingleOrDefaultAsync();
            var request = _httpContextAccessor.HttpContext!.Request;
            return new OdooTabVm
            {
                InventoryId = inventoryId,
                InventoryTitle = title,
                ApiToken = inventory?.ApiToken,
                CanManage = canManage
            };
        }
    }
}