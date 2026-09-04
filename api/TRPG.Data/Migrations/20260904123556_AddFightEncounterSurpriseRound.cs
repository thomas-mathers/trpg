using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFightEncounterSurpriseRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_surprise_round",
                table: "encounters",
                type: "boolean",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "rounds_resolved",
                table: "encounters",
                type: "integer",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "has_surprise_round", table: "encounters");

            migrationBuilder.DropColumn(name: "rounds_resolved", table: "encounters");
        }
    }
}
