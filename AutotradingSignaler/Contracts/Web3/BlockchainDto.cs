namespace AutotradingSignaler.Contracts.Web3;

public class BlockchainDto
{
    public int ChainId { get; set; }
    public string ChainName { get; set; }
    public string RpcUrl { get; set; }
    public string WssUrl { get; set; }
    public string Explorer { get; set; }
    public string Coin { get; set; }
    public BlockchainCurrency NativeCurrency { get; set; }
    public BlockchainCurrency StableCoin { get; set; }
}

public class BlockchainCurrency
{
    public string Name { get; set; }
    public string Symbol { get; set; }
    public int Decimals { get; set; }
    public string Address { get; set; } = "0x0000000000000000000000000000000000000000";
}
