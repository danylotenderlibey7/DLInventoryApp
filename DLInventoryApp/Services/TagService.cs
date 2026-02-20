using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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
                .Select(t => t!.Trim())
                .Where(t => t.Length > 0)
                .Select(t => t.ToLowerInvariant())
                .Distinct()
                .ToList();
            if (normalized.Count == 0)
            {
                var links = await _context.InventoryTags
                    .Where(l => l.InventoryId == inventoryId)
                    .ToListAsync();
                if (links.Count > 0)
                {
                    _context.InventoryTags.RemoveRange(links);
                    await _context.SaveChangesAsync();
                }
                return;
            }
            var existingTags = await _context.Tags
                .Where(t => normalized.Contains(t.Name))
                .ToListAsync();
            var existingNormalized = existingTags
                .Select(t => t.Name)
                .ToHashSet(StringComparer.Ordinal);
            var missing = normalized
                .Where(n => !existingNormalized.Contains(n))
                .ToList();
            if (missing.Count > 0)
            {
                var newTags = missing
                    .Select(n => new Tag 
                    {
                        Name = n
                    }).ToList();
                _context.Tags.AddRange(newTags);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                { }
                existingTags = await _context.Tags
                    .Where(t => normalized.Contains(t.Name))
                    .ToListAsync();
            }
            var desiredTagIds = existingTags
                .Select(t => t.Id)
                .ToHashSet();
            var currentLinks = await _context.InventoryTags
                .Where(it => it.InventoryId == inventoryId)
                .ToListAsync();
            var currentTagIds = currentLinks
                .Select(l => l.TagId)
                .ToHashSet();
            var toAddIds = desiredTagIds
                .Where(id => !currentTagIds.Contains(id))
                .ToList();
            var toRemoveLinks = currentLinks
                .Where(l => !desiredTagIds.Contains(l.TagId))
                .ToList();
            if (toAddIds.Count > 0)
            {
                _context.InventoryTags.AddRange(
                    toAddIds.Select(id => new InventoryTag { InventoryId = inventoryId, TagId = id })
                );
            }
            if (toRemoveLinks.Count > 0)
            {
                _context.InventoryTags.RemoveRange(toRemoveLinks);
            }
            if (toAddIds.Count > 0 || toRemoveLinks.Count > 0)
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}
