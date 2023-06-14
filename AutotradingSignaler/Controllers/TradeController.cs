using AutotradingSignaler.Contracts.Dtos;
using AutotradingSignaler.Core.Handlers.Commands.Web3;
using AutotradingSignaler.Core.Handlers.Queries.Web3;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutotradingSignaler.Controllers;

[ApiController]
[Route("trades")]
public class TradeController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TradeController> _logger;

    public TradeController(IMediator mediator, ILogger<TradeController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<TradeHistoryDto>> GetTradeHistory([FromQuery] GetTradeHistoryQuery query)
    {
        var response = await _mediator.Send(query);
        return Ok(response);
    }

    [HttpPost("watchlist/{address}/add")]
    public async Task<ActionResult<WalletDto>> AddToWatchlist(string address)
    {
        var query = new AddWatchlistCommand
        {
            Address = address
        };
        await _mediator.Send(query);
        return Ok();
    }

    [Authorize]
    [HttpPost("watchlist/{address}/remove")]
    public async Task<ActionResult<WalletDto>> RemoveFromWatchlist(string address)
    {
        var query = new RemoveWatchlistCommand
        {
            Address = address
        };
        await _mediator.Send(query);
        return Ok();
    }
}
