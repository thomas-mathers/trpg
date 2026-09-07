using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJailbreakCrimeType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "jailbreak_building_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "jailbreak_building_name",
                table: "crimes",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "jailbreak_outcome",
                table: "crimes",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "jailbreak_owner_faction_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );

            // Existing escapes move to the new discriminator before the flag that identified them
            // goes, or they would silently read back as ordinary break-ins.
            migrationBuilder.Sql(
                """
                UPDATE crimes
                SET jailbreak_building_id = building_id,
                    jailbreak_building_name = building_name,
                    jailbreak_owner_faction_id = lockpicking_owner_faction_id,
                    jailbreak_outcome = lockpicking_outcome,
                    crime_type = 'Jailbreak',
                    building_id = NULL,
                    building_name = NULL,
                    lockpicking_owner_faction_id = NULL,
                    lockpicking_outcome = NULL
                WHERE crime_type = 'Lockpicking' AND is_jailbreak = TRUE;
                """
            );

            migrationBuilder.DropColumn(name: "is_jailbreak", table: "crimes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "jailbreak_building_id", table: "crimes");

            migrationBuilder.DropColumn(name: "jailbreak_building_name", table: "crimes");

            migrationBuilder.DropColumn(name: "jailbreak_outcome", table: "crimes");

            migrationBuilder.DropColumn(name: "jailbreak_owner_faction_id", table: "crimes");

            migrationBuilder.AddColumn<bool>(
                name: "is_jailbreak",
                table: "crimes",
                type: "boolean",
                nullable: true
            );
        }
    }
}
