using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.Services.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

public class CustomIdGenerator : ICustomIdGenerator
{
    private readonly ApplicationDbContext _context;
    public CustomIdGenerator(ApplicationDbContext context)
    {
        _context = context;
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
