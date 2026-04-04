using CycliqueShareTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CycliqueShareTracker.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202604030001_AddExitSignalFields")]
public partial class AddExitSignalFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ExitScore",
            table: "DailySignals",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "ExitSignalLabel",
            table: "DailySignals",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "ExitPrimaryReason",
            table: "DailySignals",
            type: "character varying(512)",
            maxLength: 512,
            nullable: false,
            defaultValue: string.Empty);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ExitPrimaryReason", table: "DailySignals");
        migrationBuilder.DropColumn(name: "ExitSignalLabel", table: "DailySignals");
        migrationBuilder.DropColumn(name: "ExitScore", table: "DailySignals");
    }
}
