namespace AutotradingSignaler.Core.Web;
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using AutotradingSignaler.Contracts.Web3;
using Nethereum.Web3.Accounts;

public class Web3Service
{
    private readonly ILogger<Web3Service> _logger;
    private readonly IConfiguration _configuration;
    public static readonly Dictionary<int, BlockchainDto> Chains = new Dictionary<int, BlockchainDto>()
        {
                    {
                1, new BlockchainDto()
                {
                    ChainName = "Ethereum",
                    Coin = "ETH",
                    ChainId = 1,
                    RpcUrl = "https://eth.llamarpc.com",
                    WssUrl = "wss://main-light.eth.linkpool.io/ws",
                    Explorer = "https://etherscan.io/",
                    NativeCurrency = new BlockchainCurrency
                    {
                        Name = "Binance Coin",
                        Symbol = "BNB",
                        Decimals = 18
                    }
                }
            },
            {
                56, new BlockchainDto()
                {
                    ChainName = "Binance Smart Chain",
                    Coin = "BNB",
                    ChainId = 56,
                    RpcUrl = "https://bsc.publicnode.com",//"https://nd-335-851-551.p2pify.com/9a7159d86f3e1e7bd4517f80dadc11f3/",
                    WssUrl = "wss://ws-nd-335-851-551.p2pify.com/9a7159d86f3e1e7bd4517f80dadc11f3",
                    Explorer = "https://bscscan.com/",
                    NativeCurrency = new BlockchainCurrency
                    {
                        Name = "Binance Coin",
                        Symbol = "BNB",
                        Decimals = 18
                    }
                }
            }
        };

    private readonly Dictionary<int, Web3> _web3 = new Dictionary<int, Web3>();
    public Web3Service(ILogger<Web3Service> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        var address = configuration["OracleWallet"];
        var privateKey = configuration["OraclePrivateKey"];
        foreach (var blockchain in Chains.Values)
        {
            var account = new Account(privateKey);
            _web3.Add(blockchain.ChainId, new Web3(account, url: blockchain.RpcUrl));
        }
    }

    public Web3 GetWeb3InstanceOf(int chainId)
    {
        return _web3[chainId];
    }

    public Dictionary<int, Web3> GetWeb3Instances()
    {
        return _web3;
    }

    public BlockchainDto GetBlockchainInfoOf(int chainId)
    {
        return Chains[56];
    }

}
