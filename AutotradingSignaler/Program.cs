using AutotradingSignaler.Core.Web;
using AutotradingSignaler.Core.Web.Background;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);
var keyVaultEndpoint = new Uri(builder.Configuration["AzureKeyVaultEndpoint"]!);
builder.Configuration.AddAzureKeyVault(keyVaultEndpoint, new DefaultAzureCredential());
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<Web3Service>();
builder.Services.AddHostedService<WalletTransferBackgroundSync>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
