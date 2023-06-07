﻿using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutotradingSignaler.Contracts.Data;

[Index(nameof(Address))]
public class Watchlist : BaseEntity
{
    public string Address { get; set; }
    public string? AddedFrom { get; set; }
    public double ProfitFactor { get; set; }
    public int Trades { get; set; }
}
