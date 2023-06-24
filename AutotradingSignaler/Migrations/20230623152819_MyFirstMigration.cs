using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AutotradingSignaler.Migrations
{
    /// <inheritdoc />
    public partial class MyFirstMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Address = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Decimals = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<double>(type: "double precision", nullable: false),
                    LogoURI = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingPlattforms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Router = table.Column<string>(type: "text", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    Factory = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Fee = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingPlattforms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Watchlist",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Address = table.Column<string>(type: "text", nullable: false),
                    AddedFrom = table.Column<string>(type: "text", nullable: true),
                    ProfitFactor = table.Column<double>(type: "double precision", nullable: false),
                    Trades = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Watchlist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlattformId = table.Column<long>(type: "bigint", nullable: true),
                    TokenInId = table.Column<long>(type: "bigint", nullable: true),
                    TokenOutId = table.Column<long>(type: "bigint", nullable: true),
                    Trader = table.Column<string>(type: "text", nullable: false),
                    TokenIn = table.Column<string>(type: "text", nullable: false),
                    TokenOut = table.Column<string>(type: "text", nullable: false),
                    TokenInAmount = table.Column<double>(type: "double precision", nullable: false),
                    TokenOutAmount = table.Column<double>(type: "double precision", nullable: false),
                    TxHash = table.Column<string>(type: "text", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    TokensSold = table.Column<double>(type: "double precision", nullable: false),
                    TokenInPrice = table.Column<double>(type: "double precision", nullable: false),
                    TokenOutPrice = table.Column<double>(type: "double precision", nullable: false),
                    Profit = table.Column<double>(type: "double precision", nullable: false),
                    AverageSellPrice = table.Column<double>(type: "double precision", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trades_Tokens_TokenInId",
                        column: x => x.TokenInId,
                        principalTable: "Tokens",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Trades_Tokens_TokenOutId",
                        column: x => x.TokenOutId,
                        principalTable: "Tokens",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Trades_TradingPlattforms_PlattformId",
                        column: x => x.PlattformId,
                        principalTable: "TradingPlattforms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_Address",
                table: "Tokens",
                column: "Address");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_Address_ChainId",
                table: "Tokens",
                columns: new[] { "Address", "ChainId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_ChainId",
                table: "Tokens",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_Name",
                table: "Tokens",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_Name_ChainId",
                table: "Tokens",
                columns: new[] { "Name", "ChainId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_Symbol",
                table: "Tokens",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_Symbol_ChainId",
                table: "Tokens",
                columns: new[] { "Symbol", "ChainId" });

            migrationBuilder.CreateIndex(
                name: "IX_Trades_PlattformId",
                table: "Trades",
                column: "PlattformId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_TokenInId",
                table: "Trades",
                column: "TokenInId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_TokenOutId",
                table: "Trades",
                column: "TokenOutId");

            migrationBuilder.CreateIndex(
                name: "IX_TradingPlattforms_Factory",
                table: "TradingPlattforms",
                column: "Factory");

            migrationBuilder.CreateIndex(
                name: "IX_TradingPlattforms_Name",
                table: "TradingPlattforms",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_TradingPlattforms_Router",
                table: "TradingPlattforms",
                column: "Router");

            migrationBuilder.CreateIndex(
                name: "IX_Watchlist_Address",
                table: "Watchlist",
                column: "Address");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Trades");

            migrationBuilder.DropTable(
                name: "Watchlist");

            migrationBuilder.DropTable(
                name: "Tokens");

            migrationBuilder.DropTable(
                name: "TradingPlattforms");
        }
    }
}
