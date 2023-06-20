using System.Numerics;
using System.Text.Json;

namespace _1InchApi
{
    public class OneInchApiWrapper
    {
        private static readonly HttpClient client = new HttpClient();
        public static async Task<List<TokenData>?> GetTokens(Chain chainId)
        {
            string apiUrl = $"https://api.1inch.io/v5.0/{(int)chainId}/tokens";

            HttpResponseMessage response = await client.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"API request failed with status code {response.StatusCode}");
            }

            string responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var tokens = new Dictionary<string, TokenData>();
            foreach (var token in apiResponse.GetProperty("tokens").EnumerateObject())
            {
                string address = token.Name;
                var tokenDataRaw = token.Value.GetRawText();
                var tokenData = JsonSerializer.Deserialize<TokenData>(tokenDataRaw);
                tokens[address] = tokenData;
            }

            var tokenList = tokens.Values.ToList();
            return tokenList;
        }

        public static async Task<QuoteData?> GetQuote(Chain chainId, string fromToken, string toToken, double amount, int decimals)
        {
            var onChainAmount = new BigInteger(amount * Math.Pow(10, decimals));
            if(fromToken == "0x0000000000000000000000000000000000000000")
            {
                fromToken = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
            } else if(toToken == "0x0000000000000000000000000000000000000000")
            {
                toToken = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
            }
            string apiUrl = $"https://api.1inch.io/v5.0/{(int)chainId}/quote?" +
                $"fromTokenAddress={fromToken}" +
                $"&toTokenAddress={toToken}" +
                $"&amount={onChainAmount}";

            HttpResponseMessage response = await client.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"API request failed with status code {response.StatusCode}");
            }

            string responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<QuoteData>(responseContent);

        }
    }
}
