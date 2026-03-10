using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.ViewModels.CustomFields;
using DLInventoryApp.ViewModels.Items;
using DLInventoryApp.ViewModels.Items.Tabs;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Services.Tabs
{
    public class ItemsTabBuilder
    {
        private readonly ApplicationDbContext _context;

        public ItemsTabBuilder(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<InventoryItemsVm> BuildAsync(Guid inventoryId, string inventoryTitle, bool canEditItems, string? currentUserId)
        {
            var items = await _context.Items
                .Where(it => it.InventoryId == inventoryId)
                .Select(it => new InventoryItemRowVm
                {
                    Id = it.Id,
                    CustomId = it.CustomId,
                    CreatedAt = it.CreatedAt,
                    UpdatedAt = it.UpdatedAt,
                    Version = it.Version
                })
                .OrderByDescending(vm => vm.UpdatedAt ?? vm.CreatedAt)
                .ToListAsync();
            var cols = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId)
                .OrderBy(f => f.Order)
                .Select(f => new CustomFieldColumnVm
                {
                    Id = f.Id,
                    Name = f.Name,
                    Order = f.Order,
                    IsRequired = f.IsRequired,
                    IsUnique = f.IsUnique,
                    Type = f.Type
                }).ToListAsync();
            var itemIds = items.Select(x => x.Id).ToList();
            var valuesByItem = new Dictionary<Guid, List<ItemFieldValue>>();
            var likesCountByItem = new Dictionary<Guid, int>();
            var likedSet = new HashSet<Guid>();
            if (itemIds.Count > 0)
            {
                var allValues = await _context.ItemFieldValues
                    .Where(v => itemIds.Contains(v.ItemId))
                    .ToListAsync();
                valuesByItem = allValues
                    .GroupBy(v => v.ItemId)
                    .ToDictionary(g => g.Key, g => g.ToList());
                likesCountByItem = await _context.ItemLikes
                    .Where(l => itemIds.Contains(l.ItemId))
                    .GroupBy(l => l.ItemId)
                    .Select(g => new { ItemId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ItemId, x => x.Count);
                if (currentUserId != null)
                {
                    var likedList = await _context.ItemLikes
                        .Where(l => l.UserId == currentUserId && itemIds.Contains(l.ItemId))
                        .Select(l => l.ItemId)
                        .ToListAsync();
                    likedSet = likedList.ToHashSet();
                }
            }
            foreach (var it in items)
            {
                it.LikesCount = likesCountByItem.TryGetValue(it.Id, out var c) ? c : 0;
                it.IsLikedByMe = currentUserId != null && likedSet.Contains(it.Id);
                it.Cells = new List<string?>();
                valuesByItem.TryGetValue(it.Id, out var values);
                foreach (var col in cols)
                {
                    var cell = values?.FirstOrDefault(v => v.CustomFieldId == col.Id);
                    string? cellText = null;
                    if (cell != null)
                    {
                        if (cell.TextValue != null) cellText = cell.TextValue;
                        else if (cell.NumberValue != null) cellText = cell.NumberValue.Value.ToString();
                        else if (cell.LinkValue != null) cellText = cell.LinkValue;
                        else if (cell.BoolValue != null) cellText = cell.BoolValue.Value ? "Yes" : "No";
                    }
                    it.Cells.Add(cellText);
                }
            }
            return new InventoryItemsVm
            {
                InventoryId = inventoryId,
                InventoryTitle = inventoryTitle,
                Items = items,
                Columns = cols,
                CanEditItems = canEditItems
            };
        }
    }
}