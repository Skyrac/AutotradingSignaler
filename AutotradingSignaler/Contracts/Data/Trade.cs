using System.ComponentModel.DataAnnotations.Schema;

namespace AutotradingSignaler.Contracts.Data;

public class Trade : BaseEntity
{
    public string Trader { get; set; }
    public string Plattform { get; set; }
    public string TokenIn { get; set; }
    public string TokenOut { get; set; }
    public decimal TokenInAmount { get; set; }
    public decimal TokenOutAmount { get; set; }
    public string TxHash { get; set; }
    public int ChainId { get; set; }
    public bool IsBuy { get; set; }
    public double Profit { get; set; }
}
