using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitLockpickingAndTrespassingCrimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing BreakEnter rows are lockpicking offences, so the column carries over to it.
            migrationBuilder.RenameColumn(
                name: "break_enter_owner_faction_id",
                table: "crimes",
                newName: "lockpicking_owner_faction_id"
            );

            migrationBuilder.Sql(
                "UPDATE crimes SET crime_type = 'Lockpicking' WHERE crime_type = 'BreakEnter';"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "triggering_crime_id",
                table: "encounters",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "lockpicking_outcome",
                table: "crimes",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "trespassing_owner_faction_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "trespassing_building_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "trespassing_building_name",
                table: "crimes",
                type: "text",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM crimes WHERE crime_type = 'Trespassing';");

            migrationBuilder.Sql(
                "UPDATE crimes SET crime_type = 'BreakEnter' WHERE crime_type = 'Lockpicking';"
            );

            migrationBuilder.DropColumn(name: "triggering_crime_id", table: "encounters");

            migrationBuilder.DropColumn(name: "lockpicking_outcome", table: "crimes");

            migrationBuilder.DropColumn(name: "trespassing_owner_faction_id", table: "crimes");

            migrationBuilder.DropColumn(name: "trespassing_building_id", table: "crimes");

            migrationBuilder.DropColumn(name: "trespassing_building_name", table: "crimes");

            migrationBuilder.RenameColumn(
                name: "lockpicking_owner_faction_id",
                table: "crimes",
                newName: "break_enter_owner_faction_id"
            );
        }
    }
}
