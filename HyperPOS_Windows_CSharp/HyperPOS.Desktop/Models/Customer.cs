namespace HyperPOS.Desktop.Models;

public sealed class Customer
{
    public long Id { get; init; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
