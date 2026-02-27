using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.Services.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
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
            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                try
                {
                    var sequence = await _context.InventorySequences
                        .Where(s => s.InventoryId == inventoryId)
                        .FirstOrDefaultAsync();
                    if (sequence == null)
                        throw new InvalidOperationException("Sequence not configured for inventory.");
                    nextSequence = sequence.NextValue;
                    sequence.NextValue++;
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                    break;
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "40001")
                {
                    await tx.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    if (attempt == maxAttempts) throw;
                }
                catch (PostgresException pg) when (pg.SqlState == "40001")
                {
                    await tx.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    if (attempt == maxAttempts) throw;
                }
            }
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
    public async Task<CustomIdResult> PreviewAsync(Guid inventoryId)
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
            var sequence = await _context.InventorySequences
                .Where(s => s.InventoryId == inventoryId)
                .FirstOrDefaultAsync();
            if (sequence == null)
                throw new InvalidOperationException("Sequence not configured for inventory.");
            nextSequence = sequence.NextValue;
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
                default:
                    throw new InvalidOperationException($"Unsupported CustomId element type: {el.Type}");
            }
        }
        return new CustomIdResult
        {
            CustomId = builder.ToString(),
            SequenceNumber = nextSequence
        };
    }
}
