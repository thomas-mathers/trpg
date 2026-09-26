using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureEngagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "paused_at_game_time",
                table: "route_travelers",
                type: "timestamp without time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "is_engaged",
                table: "creatures",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "paused_at_game_time", table: "route_travelers");

            migrationBuilder.DropColumn(name: "is_engaged", table: "creatures");
        }
    }
}
