using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureCombatResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "current_ap",
                table: "creatures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "current_hp",
                table: "creatures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "current_mp",
                table: "creatures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "last_regen_playtime",
                table: "creatures",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "current_ap",
                table: "creatures");

            migrationBuilder.DropColumn(
                name: "current_hp",
                table: "creatures");

            migrationBuilder.DropColumn(
                name: "current_mp",
                table: "creatures");

            migrationBuilder.DropColumn(
                name: "last_regen_playtime",
                table: "creatures");
        }
    }
}
