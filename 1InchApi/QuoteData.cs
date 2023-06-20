using System.Numerics;

namespace _1InchApi;

public record QuoteData(TokenData fromToken, TokenData toToken, string toTokenAmount, string fromTokenAmount, long estimatedGas);
