using CycliqueShareTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CycliqueShareTracker.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202604160001_AddStockIndicatorSettings")]
public partial class AddStockIndicatorSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StockIndicatorSettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ParabolicSarStep = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                ParabolicSarMax = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                BollingerPeriod = table.Column<int>(type: "integer", nullable: false),
                BollingerStdDev = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                MacdFastPeriod = table.Column<int>(type: "integer", nullable: false),
                MacdSlowPeriod = table.Column<int>(type: "integer", nullable: false),
                MacdSignalPeriod = table.Column<int>(type: "integer", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockIndicatorSettings", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StockIndicatorSettings_Symbol",
            table: "StockIndicatorSettings",
            column: "Symbol",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StockIndicatorSettings");
    }
}
