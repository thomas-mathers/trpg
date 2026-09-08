using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class PropertyCrimesWrongEveryFaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "jailbreak_owner_faction_ids",
                table: "crimes",
                type: "uuid[]",
                nullable: true
            );

            migrationBuilder.AddColumn<List<Guid>>(
                name: "lockpicking_owner_faction_ids",
                table: "crimes",
                type: "uuid[]",
                nullable: true
            );

            migrationBuilder.AddColumn<List<Guid>>(
                name: "trespassing_owner_faction_ids",
                table: "crimes",
                type: "uuid[]",
                nullable: true
            );

            // Carry the owner across and add the city that always had a stake, so existing
            // break-ins gain the faction the singular column could never hold.
            migrationBuilder.Sql(
                """
                UPDATE crimes c
                SET lockpicking_owner_faction_ids = array_remove(
                    ARRAY[
                        c.lockpicking_owner_faction_id,
                        (SELECT f.id FROM factions f
                          WHERE f.is_city_faction
                            AND f.world_id = c.world_id
                            AND f.city_id = c.city_id
                          LIMIT 1)
                    ], NULL)
                WHERE c.crime_type = 'Lockpicking';
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE crimes c
                SET jailbreak_owner_faction_ids = array_remove(
                    ARRAY[
                        c.jailbreak_owner_faction_id,
                        (SELECT f.id FROM factions f
                          WHERE f.is_city_faction
                            AND f.world_id = c.world_id
                            AND f.city_id = c.city_id
                          LIMIT 1)
                    ], NULL)
                WHERE c.crime_type = 'Jailbreak';
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE crimes c
                SET trespassing_owner_faction_ids = array_remove(
                    ARRAY[
                        c.trespassing_owner_faction_id,
                        (SELECT f.id FROM factions f
                          WHERE f.is_city_faction
                            AND f.world_id = c.world_id
                            AND f.city_id = c.city_id
                          LIMIT 1)
                    ], NULL)
                WHERE c.crime_type = 'Trespassing';
                """
            );

            migrationBuilder.DropColumn(name: "lockpicking_owner_faction_id", table: "crimes");
            migrationBuilder.DropColumn(name: "jailbreak_owner_faction_id", table: "crimes");
            migrationBuilder.DropColumn(name: "trespassing_owner_faction_id", table: "crimes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "jailbreak_owner_faction_ids", table: "crimes");

            migrationBuilder.DropColumn(name: "lockpicking_owner_faction_ids", table: "crimes");

            migrationBuilder.DropColumn(name: "trespassing_owner_faction_ids", table: "crimes");

            migrationBuilder.AddColumn<Guid>(
                name: "jailbreak_owner_faction_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "lockpicking_owner_faction_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "trespassing_owner_faction_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );
        }
    }
}
