using System.ComponentModel.DataAnnotations.Schema;

namespace AutotradingSignaler.Contracts.Data;

public class Trade : BaseEntity
{
    [ForeignKey(nameof(Plattform))]
    public long? PlattformId { get; set; }
    [ForeignKey(nameof(TokenInData))]
    public long? TokenInId { get; set; }
    [ForeignKey(nameof(TokenOutData))]
    public long? TokenOutId { get; set; }
    public string Trader { get; set; }
    public string TokenIn { get; set; }
    public string TokenOut { get; set; }
    public double TokenInAmount { get; set; }
    public double TokenOutAmount { get; set; }
    public string TxHash { get; set; }
    public int ChainId { get; set; }
    public double TokensSold { get; set; } = 0;
    public double TokenInPrice { get; set; }
    public double TokenOutPrice { get; set; }
    public double Profit { get; set; }
    public double AverageSellPrice { get; set; } = 0;
    public TradingPlattform? Plattform { get; set; }
    public Token TokenInData { get; set; }
    public Token TokenOutData { get; set; }
}
