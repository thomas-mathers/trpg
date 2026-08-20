using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardEncounterAndDoorTimedUnlock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "guard_creature_id",
                table: "encounters",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "unlocks_at_playtime",
                table: "door_connectors",
                type: "interval",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "guard_creature_id", table: "encounters");

            migrationBuilder.DropColumn(name: "unlocks_at_playtime", table: "door_connectors");
        }
    }
}
