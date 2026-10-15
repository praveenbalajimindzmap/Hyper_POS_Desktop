using System;

namespace HyperPOS.Desktop.Models;

public sealed class HeldOrderSummary
{
    public long Id { get; init; }
    public string CustomerName { get; init; } = "";
    public DateTime HeldAt { get; init; }
    public int ItemCount { get; init; }
    public decimal Total { get; init; }

    public string CustomerInitials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CustomerName)) return "WC";
            var parts = CustomerName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpperInvariant() : parts[0].ToUpperInvariant();
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }
    }

    public string BadgeColor
    {
        get
        {
            var colors = new[] { "#3B82F6", "#8B5CF6", "#EC4899", "#10B981", "#F59E0B", "#06B6D4", "#6366F1" };
            var hash = Math.Abs(CustomerName.GetHashCode() + (int)Id);
            return colors[hash % colors.Length];
        }
    }

    public string TimeAgo
    {
        get
        {
            var diff = DateTime.Now - HeldAt;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            return HeldAt.ToString("dd MMM, hh:mm tt");
        }
    }
}
