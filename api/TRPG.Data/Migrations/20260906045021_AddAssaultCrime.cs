using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssaultCrime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "assault_victim_faction_ids",
                table: "crimes",
                type: "uuid[]",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "assault_victim_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "assault_victim_name",
                table: "crimes",
                type: "text",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "assault_victim_faction_ids", table: "crimes");

            migrationBuilder.DropColumn(name: "assault_victim_id", table: "crimes");

            migrationBuilder.DropColumn(name: "assault_victim_name", table: "crimes");
        }
    }
}
