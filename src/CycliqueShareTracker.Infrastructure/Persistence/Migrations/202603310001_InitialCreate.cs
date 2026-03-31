using System;
using CycliqueShareTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CycliqueShareTracker.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202603310001_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Assets",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Market = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Assets", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DailyIndicators",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AssetId = table.Column<int>(type: "integer", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Sma50 = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                Sma200 = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                Rsi14 = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                Drawdown52WeeksPercent = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DailyIndicators", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DailyPrices",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AssetId = table.Column<int>(type: "integer", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Open = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                High = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                Low = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                Close = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                Volume = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DailyPrices", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DailySignals",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AssetId = table.Column<int>(type: "integer", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Score = table.Column<int>(type: "integer", nullable: false),
                SignalLabel = table.Column<int>(type: "integer", nullable: false),
                Explanation = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DailySignals", x => x.Id);
            });

        migrationBuilder.CreateIndex(name: "IX_Assets_Symbol", table: "Assets", column: "Symbol", unique: true);
        migrationBuilder.CreateIndex(name: "IX_DailyIndicators_AssetId_Date", table: "DailyIndicators", columns: new[] { "AssetId", "Date" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_DailyPrices_AssetId_Date", table: "DailyPrices", columns: new[] { "AssetId", "Date" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_DailySignals_AssetId_Date", table: "DailySignals", columns: new[] { "AssetId", "Date" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DailyIndicators");
        migrationBuilder.DropTable(name: "DailyPrices");
        migrationBuilder.DropTable(name: "DailySignals");
        migrationBuilder.DropTable(name: "Assets");
    }
}
