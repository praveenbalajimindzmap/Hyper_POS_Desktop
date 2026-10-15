namespace HyperPOS.Desktop.Models;

public sealed class PosShift
{
    public long Id { get; init; }
    public DateTime BusinessDate { get; init; }
    public DateTime OpenedAt { get; init; }
    public decimal OpeningCash { get; init; }
}
