using CycliqueShareTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CycliqueShareTracker.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202604040002_AddEmaIndicators")]
public partial class AddEmaIndicators : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "Ema12",
            table: "DailyIndicators",
            type: "numeric(18,4)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Ema26",
            table: "DailyIndicators",
            type: "numeric(18,4)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Ema12", table: "DailyIndicators");
        migrationBuilder.DropColumn(name: "Ema26", table: "DailyIndicators");
    }
}
