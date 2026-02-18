using DLInventoryApp.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

public class CustomIdGenerator : ICustomIdGenerator
{
    public string Generate(string title, IReadOnlyCollection<string> existingCustomIds)
    {
        var prefix = BuildPrefix(title);
        var max = 0;
        foreach (var id in existingCustomIds)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;
            var parts = id.Split('-', 2);
            if (parts.Length != 2)
                continue;
            if (!string.Equals(parts[0], prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (int.TryParse(parts[1], out var n) && n > max)
                max = n;
        }
        var next = max + 1;
        return $"{prefix}-{next:D3}";
    }
    private string BuildPrefix(string title)
    {
        title = (title ?? string.Empty).Trim();
        var words = title
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
            return "INV"; 
        var first = TakeUpTo3Letters(words[0]);
        var last = (words.Length == 1) ? "" : TakeUpTo3Letters(words[words.Length - 1]);
        var prefix = (first + last).ToUpperInvariant();
        if (prefix.Length < 3)
            prefix = prefix.PadRight(3, 'X');
        return prefix;
    }
    private string TakeUpTo3Letters(string s)
    {
        s = (s ?? string.Empty).Trim();
        return s.Length <= 3 ? s : s.Substring(0, 3);
    }
}
