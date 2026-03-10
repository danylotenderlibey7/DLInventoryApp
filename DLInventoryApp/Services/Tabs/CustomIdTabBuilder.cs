using DLInventoryApp.Data;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.CustomId;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Services.Tabs
{
    public class CustomIdTabBuilder
    {
        private readonly ApplicationDbContext _context;
        private readonly ICustomIdGenerator _customIdGenerator;
        public CustomIdTabBuilder(ApplicationDbContext context, ICustomIdGenerator customIdGenerator)
        {
            _context = context;
            _customIdGenerator = customIdGenerator;
        }
        public async Task<CustomIdIndexVm> BuildAsync(Guid inventoryId, string inventoryTitle, bool canManage)
        {
            var elements = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId)
                .OrderBy(e => e.Order)
                .Select(e => new CustomIdElementRowVm
                {
                    Id = e.Id,
                    Order = e.Order,
                    Type = e.Type,
                    Text = e.Text,
                    Format = e.Format
                }).ToListAsync();
            string preview;
            try
            {
                var result = await _customIdGenerator.PreviewAsync(inventoryId);
                preview = result.CustomId;
            }
            catch
            {
                preview = "(no template)";
            }
            return new CustomIdIndexVm
            {
                InventoryId = inventoryId,
                InventoryTitle = inventoryTitle,
                CanManage = canManage,
                Preview = preview,
                Elements = elements
            };
        }
    }
}