using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrimeCityAndSettlementAndFactionCity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "city_id",
                table: "factions",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "city_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "settled_at",
                table: "crimes",
                type: "timestamp with time zone",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "city_id", table: "factions");

            migrationBuilder.DropColumn(name: "city_id", table: "crimes");

            migrationBuilder.DropColumn(name: "settled_at", table: "crimes");
        }
    }
}
