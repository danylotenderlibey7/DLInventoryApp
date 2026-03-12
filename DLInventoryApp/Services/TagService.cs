using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Services
{
    public class TagService : ITagService
    {
        private readonly ApplicationDbContext _context;
        public TagService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task SyncInventoryTagsAsync(Guid inventoryId, IEnumerable<string> tags)
        {
            var normalized = (tags ?? Enumerable.Empty<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant())
                .Distinct()
                .Take(30)
                .ToList();
            var currentLinks = await _context.InventoryTags
                .Include(it => it.Tag)
                .Where(it => it.InventoryId == inventoryId)
                .ToListAsync();
            if (normalized.Count == 0)
            {
                if (currentLinks.Count > 0)
                    _context.InventoryTags.RemoveRange(currentLinks);
                return;
            }
            var existingTags = await _context.Tags
                .Where(t => normalized.Contains(t.Name))
                .ToListAsync();
            var existingNames = existingTags
                .Select(t => t.Name)
                .ToHashSet(StringComparer.Ordinal);
            var missingNames = normalized
                .Where(name => !existingNames.Contains(name))
                .ToList();
            var newTags = missingNames
                .Select(name => new Tag { Name = name })
                .ToList();
            if (newTags.Count > 0) _context.Tags.AddRange(newTags);
            var allDesiredTags = existingTags.Concat(newTags).ToList();
            var currentNames = currentLinks
                .Select(x => x.Tag.Name)
                .ToHashSet(StringComparer.Ordinal);
            var linksToAdd = allDesiredTags
                .Where(t => !currentNames.Contains(t.Name))
                .Select(t => new InventoryTag
                {
                    InventoryId = inventoryId,
                    Tag = t
                }).ToList();
            var linksToRemove = currentLinks
                .Where(link => !normalized.Contains(link.Tag.Name))
                .ToList();
            if (linksToAdd.Count > 0) _context.InventoryTags.AddRange(linksToAdd);
            if (linksToRemove.Count > 0) _context.InventoryTags.RemoveRange(linksToRemove);
        }
    }
}