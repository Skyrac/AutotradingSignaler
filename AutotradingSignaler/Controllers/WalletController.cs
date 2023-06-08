using AutotradingSignaler.Contracts.Dtos;
using AutotradingSignaler.Core.Handlers.Queries.Web3;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutotradingSignaler.Controllers;

[ApiController]
[Route("wallet")]
public class WalletController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WalletController> _logger;

    public WalletController(IMediator mediator, ILogger<WalletController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("{address}")]
    public async Task<ActionResult<WalletDto>> GetWalletBalances(string address)
    {
        var query = new GetWalletInformationQuery() { Address = address };
        var response = await _mediator.Send(query);
        return Ok(response);
    }
}
