﻿using AutotradingSignaler.Contracts.Dtos;
using AutotradingSignaler.Core.Handlers.Commands.Web3;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutotradingSignaler.Controllers;

[ApiController]
[Route("tokens")]
public class TokenController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TokenController> _logger;

    public TokenController(IMediator mediator, ILogger<TokenController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("{address}/add")]
    public async Task<ActionResult<TokenDto>> AddToken(string address)
    {
        var query = new AddTokenCommand
        {
            Address = address
        };
        var response = await _mediator.Send(query);
        return Ok(response?.Adapt<TokenDto>());
    }
}
