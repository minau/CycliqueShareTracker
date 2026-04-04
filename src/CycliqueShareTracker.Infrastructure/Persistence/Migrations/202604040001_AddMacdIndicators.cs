using CycliqueShareTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CycliqueShareTracker.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202604040001_AddMacdIndicators")]
public partial class AddMacdIndicators : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "MacdHistogram",
            table: "DailyIndicators",
            type: "numeric(18,4)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "MacdLine",
            table: "DailyIndicators",
            type: "numeric(18,4)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "MacdSignalLine",
            table: "DailyIndicators",
            type: "numeric(18,4)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "MacdHistogram", table: "DailyIndicators");
        migrationBuilder.DropColumn(name: "MacdLine", table: "DailyIndicators");
        migrationBuilder.DropColumn(name: "MacdSignalLine", table: "DailyIndicators");
    }
}
