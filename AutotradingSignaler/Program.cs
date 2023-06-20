using AutotradingSignaler.Core.Web;
using AutotradingSignaler.Core.Web.Background;
using Azure.Identity;
using AutotradingSignaler.Core.Extensions;
using AutotradingSignaler.Persistence;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3;
using AutotradingSignaler.Core.Services.Interfaces;
using AutotradingSignaler.Core.Services;
using AutotradingSignaler.Core.Web3.Background;

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
//builder.Services.AddHostedService<WalletTransferBackgroundSync>();
builder.Services.AddHostedService<TokenPriceUpdaterBackgroundService>();



builder.Services.AddPersistence<BaseMigrationDbContext, IWeb3UnitOfWork, Web3UnitOfWork>(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

}
app.UseCors(builder =>
{
    if (app.Environment.IsDevelopment())
    {
        builder.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod();
    }
});
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
