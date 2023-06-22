using AutotradingSignaler.Contracts.Data;
using Mapster;

namespace AutotradingSignaler.Contracts.Dtos
{
    [AdaptFrom(typeof(TradingPlattform))]
    public class TradingPlattformDto
    {
        public string? Name { get; set; }
        public string Router { get; set; }
        public int ChainId { get; set; }
        public string? Factory { get; set; }
        public PlattformVersion Version { get; set; }
        public int Fee { get; set; } = 500;
    }
}
