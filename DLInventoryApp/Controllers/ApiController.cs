using DLInventoryApp.Data;
using DLInventoryApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public ApiController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet("inventory/{token}/results")]
        public async Task<IActionResult> GetResults(string token)
        {
            var inventory = await _context.Inventories
                .AsNoTracking()
                .Include(i => i.CustomFields)
                .FirstOrDefaultAsync(i => i.ApiToken == token);
            if (inventory == null) return Unauthorized(new { error = "Invalid or expired token." });
            var itemsCount = await _context.Items
                .AsNoTracking()
                .CountAsync(x => x.InventoryId == inventory.Id);
            var values = await _context.ItemFieldValues
                .AsNoTracking()
                .Where(v => v.Item.InventoryId == inventory.Id)
                .Select(v => new
                {
                    v.CustomFieldId,
                    v.TextValue,
                    v.NumberValue,
                    v.LinkValue,
                    v.BoolValue
                }).ToListAsync();
            var orderedFields = inventory.CustomFields
                .OrderBy(f => f.Order)
                .ToList();
            var fields = orderedFields
                .Select(f => new
                {
                    field_id = f.Id,
                    name = f.Name,
                    type = f.Type.ToString()
                }).ToList();
            var aggregates = new List<object>();
            foreach (var field in orderedFields)
            {
                if (field.Type == CustomFieldType.Number)
                {
                    var nums = values
                        .Where(v => v.CustomFieldId == field.Id && v.NumberValue.HasValue)
                        .Select(v => Convert.ToDecimal(v.NumberValue!.Value))
                        .ToList();
                    aggregates.Add(new
                    {
                        field_id = field.Id,
                        field_name = field.Name,
                        field_type = field.Type.ToString(),
                        metric_type = "number",
                        count = nums.Count,
                        min = nums.Count > 0 ? nums.Min() : (decimal?)null,
                        max = nums.Count > 0 ? nums.Max() : (decimal?)null,
                        avg = nums.Count > 0 ? nums.Average() : (decimal?)null
                    });
                    continue;
                }
                if (field.Type == CustomFieldType.Boolean)
                {
                    var bools = values
                        .Where(v => v.CustomFieldId == field.Id && v.BoolValue.HasValue)
                        .Select(v => v.BoolValue!.Value)
                        .ToList();
                    aggregates.Add(new
                    {
                        field_id = field.Id,
                        field_name = field.Name,
                        field_type = field.Type.ToString(),
                        metric_type = "boolean",
                        count = bools.Count,
                        true_count = bools.Count(x => x),
                        false_count = bools.Count(x => !x)
                    });
                    continue;
                }
                if (field.Type == CustomFieldType.SingleLineText || field.Type == CustomFieldType.MultiLineText)
                {
                    var topValues = values
                        .Where(v => v.CustomFieldId == field.Id && !string.IsNullOrWhiteSpace(v.TextValue))
                        .Select(v => v.TextValue!.Trim())
                        .GroupBy(x => x)
                        .Select(g => new
                        {
                            value = g.Key,
                            count = g.Count()
                        })
                        .OrderByDescending(x => x.count)
                        .ThenBy(x => x.value)
                        .Take(5)
                        .ToList();
                    aggregates.Add(new
                    {
                        field_id = field.Id,
                        field_name = field.Name,
                        field_type = field.Type.ToString(),
                        metric_type = "top_values",
                        top_values = topValues
                    });
                    continue;
                }
                if (field.Type == CustomFieldType.DocumentLink)
                {
                    var topLinks = values
                        .Where(v => v.CustomFieldId == field.Id && !string.IsNullOrWhiteSpace(v.LinkValue))
                        .Select(v => v.LinkValue!.Trim())
                        .GroupBy(x => x)
                        .Select(g => new
                        {
                            value = g.Key,
                            count = g.Count()
                        })
                        .OrderByDescending(x => x.count)
                        .ThenBy(x => x.value)
                        .Take(5)
                        .ToList();
                    aggregates.Add(new
                    {
                        field_id = field.Id,
                        field_name = field.Name,
                        field_type = field.Type.ToString(),
                        metric_type = "top_values",
                        top_values = topLinks
                    });
                }
            }
            return Ok(new
            {
                inventory_id = inventory.Id,
                title = inventory.Title,
                description = inventory.Description,
                items_count = itemsCount,
                fields,
                aggregates
            });
        }
    }
}