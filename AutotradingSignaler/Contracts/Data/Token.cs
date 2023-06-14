using Microsoft.EntityFrameworkCore;

namespace AutotradingSignaler.Contracts.Data;

[Index(nameof(Name), nameof(ChainId))]
[Index(nameof(Address), nameof(ChainId))]
[Index(nameof(Symbol), nameof(ChainId))]
[Index(nameof(Address))]
[Index(nameof(ChainId))]
[Index(nameof(Name))]
[Index(nameof(Symbol))]
public class Token : BaseEntity
{
    public string Address { get; set; }
    public string Name { get; set; }
    public string Symbol { get; set; }
    public int Decimals { get; set; }
    public int ChainId { get; set; }
    public string? LogoURI { get; set; }
}
