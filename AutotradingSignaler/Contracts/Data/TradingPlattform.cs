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
        public PlattformVersion Version { get; set; }
        public int Fee { get; set; } = 500;
    }

    public enum PlattformVersion
    {
        V1,
        V2,
        V3
    }
}
