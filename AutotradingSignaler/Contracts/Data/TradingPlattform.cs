using Microsoft.EntityFrameworkCore;

namespace AutotradingSignaler.Contracts.Data
{
    [Index(nameof(Name))]
    [Index(nameof(Router))]
    [Index(nameof(Factory))]
    public class TradingPlattform : BaseEntity
    {
        public string? Name { get; set; }
        public string Router { get; set; }
        public int ChainId { get; set; }
        public bool IsValid { get; set; } = true;
        public string? Factory { get; set; }
    }
}
