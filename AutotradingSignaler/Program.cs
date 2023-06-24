using AutotradingSignaler.Core.Extensions;
using AutotradingSignaler.Core.Services;
using AutotradingSignaler.Core.Services.Interfaces;
using AutotradingSignaler.Core.Web;
using AutotradingSignaler.Core.Web.Background;
using AutotradingSignaler.Core.Web3.Background;
using AutotradingSignaler.Persistence;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var keyVaultEndpoint = new Uri(builder.Configuration["AzureKeyVaultEndpoint"]!);
builder.Configuration.AddAzureKeyVault(keyVaultEndpoint, new DefaultAzureCredential());
// Add services to the container.
builder.Services.AddCors();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddSingleton<Web3Service>();
builder.Services.AddHostedService<WalletTransferBackgroundSync>();
builder.Services.AddHostedService<TokenPriceUpdaterBackgroundService>();
CreateDatabaseContext(builder);

var app = builder.Build();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<BaseMigrationDbContext>().Database.Migrate();
}
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

}
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(builder =>
{
    if (app.Environment.IsDevelopment())
    {
        builder.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod();
    }
    else
    {
        builder.AllowAnyHeader().AllowAnyMethod();
        builder.WithOrigins("https://trader.kryptoflow.de");
        builder.WithOrigins("https://host.talkaboat.online");
    }
});
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

static void CreateDatabaseContext(WebApplicationBuilder builder)
{
    builder.Services.AddPersistence<BaseMigrationDbContext, IWeb3UnitOfWork, Web3UnitOfWork>(builder.Configuration);

}
