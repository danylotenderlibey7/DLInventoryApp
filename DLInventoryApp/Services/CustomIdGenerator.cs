using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.Services.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

public class CustomIdGenerator : ICustomIdGenerator
{
    private readonly ApplicationDbContext _context;
    public CustomIdGenerator(ApplicationDbContext context)
    {
        _context = context;
    }
    public async Task<bool> MatchesTemplateAsync(Guid inventoryId, string customId)
    {
        if (string.IsNullOrWhiteSpace(customId))
            return false;

        var elements = await _context.CustomIdElements
            .Where(e => e.InventoryId == inventoryId)
            .OrderBy(e => e.Order)
            .ToListAsync();

        if (!elements.Any())
            return false;

        var pattern = BuildRegexPattern(elements);
        return Regex.IsMatch(customId, pattern);
    }

    private static string BuildRegexPattern(IEnumerable<InventoryCustomIdElement> elements)
    {
        var builder = new StringBuilder("^");
        foreach (var el in elements)
        {
            builder.Append(el.Type switch
            {
                CustomIdElementType.FixedText => Regex.Escape(el.Text ?? string.Empty),
                CustomIdElementType.Sequence => SequencePattern(el.Format),
                CustomIdElementType.DateTime => DateTimePattern(el.Format),
                CustomIdElementType.Guid => GuidPattern(el.Format),
                CustomIdElementType.Random6Digits => @"\d{6}",
                CustomIdElementType.Random9Digits => @"\d{9}",
                CustomIdElementType.Random20Bit => @"\d+",
                CustomIdElementType.Random32Bit => @"\d+",
                _ => throw new InvalidOperationException($"Unsupported CustomId element type: {el.Type}")
            });
        }

        builder.Append("$");
        return builder.ToString();
    }

    private static string SequencePattern(string? format)
    {
        if (!string.IsNullOrWhiteSpace(format)
            && format.StartsWith("D", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(format.AsSpan(1), out var width)
            && width > 0)
        {
            return $@"\d{{{width}}}";
        }

        return @"\d+";
    }

    private static string DateTimePattern(string? format)
    {
        var actualFormat = string.IsNullOrWhiteSpace(format) ? "yyyyMMddHHmmss" : format;
        var regex = Regex.Escape(actualFormat)
            .Replace("yyyy", @"\d{4}")
            .Replace("yy", @"\d{2}")
            .Replace("MM", @"\d{2}")
            .Replace("dd", @"\d{2}")
            .Replace("HH", @"\d{2}")
            .Replace("mm", @"\d{2}")
            .Replace("ss", @"\d{2}");

        return regex;
    }

    private static string GuidPattern(string? format)
    {
        var f = string.IsNullOrWhiteSpace(format) ? "N" : format;
        return f.ToUpperInvariant() switch
        {
            "N" => @"[0-9a-fA-F]{32}",
            "D" => @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            "B" => @"\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}",
            "P" => @"\([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\)",
            _ => @"[0-9a-fA-F]{32}"
        };
    }
    public async Task<CustomIdResult> GenerateAsync(Guid inventoryId)
    {
        var elements = await _context.CustomIdElements
            .Where(e => e.InventoryId == inventoryId)
            .OrderBy(e => e.Order)
            .ToListAsync();
        if (!elements.Any()) throw new InvalidOperationException("Custom ID template not configured.");
        var hasSequence = elements.Any(e => e.Type == CustomIdElementType.Sequence);
        int? nextSequence = null;
        if (hasSequence)
        {
            var maxSequence = await _context.Items
                .Where(i => i.InventoryId == inventoryId)
                .MaxAsync(i => (int?)i.SequenceNumber);
            nextSequence = maxSequence == null ? 1 : maxSequence.Value + 1;
        }
        var builder = new StringBuilder();
        foreach (var el in elements)
        {
            switch (el.Type)
            {
                case CustomIdElementType.FixedText:
                    builder.Append(el.Text);
                    break;
                case CustomIdElementType.Sequence:
                    builder.Append(nextSequence!.Value.ToString(el.Format ?? "D"));
                    break;
                case CustomIdElementType.DateTime:
                    builder.Append(DateTime.UtcNow.ToString(el.Format ?? "yyyyMMddHHmmss"));
                    break;
                case CustomIdElementType.Guid:
                    builder.Append(Guid.NewGuid().ToString(el.Format ?? "N"));
                    break;
                case CustomIdElementType.Random6Digits:
                    builder.Append(Random.Shared.Next(0, 1_000_000).ToString("D6"));
                    break;
                case CustomIdElementType.Random9Digits:
                    builder.Append(Random.Shared.Next(0, 1_000_000_000).ToString("D9"));
                    break;
                case CustomIdElementType.Random20Bit:
                    builder.Append(Random.Shared.Next(0, 1 << 20));
                    break;
                case CustomIdElementType.Random32Bit:
                    builder.Append(Random.Shared.NextInt64(0, 1L << 32).ToString());
                    break;
                default: throw new InvalidOperationException($"Unsupported CustomId element type: {el.Type}");
            }
        }
        return new CustomIdResult
        {
            CustomId = builder.ToString(),
            SequenceNumber = nextSequence
        };
    }
}
