using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTheftCrimeAndPickpocketing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "items",
                table: "crimes",
                type: "jsonb",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "outcome",
                table: "crimes",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "owner_creature_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "owner_faction_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "owner_name",
                table: "crimes",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "source_owner_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "source_owner_type",
                table: "crimes",
                type: "text",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                INSERT INTO creature_skills (id, creature_id, experience, level, skill, world_id)
                SELECT
                    md5(creature.id::text || ':Pickpocketing')::uuid,
                    creature.id,
                    0,
                    CASE WHEN world.player_id = creature.id THEN 1 ELSE 0 END,
                    'Pickpocketing',
                    creature.world_id
                FROM creatures AS creature
                JOIN worlds AS world ON world.id = creature.world_id
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM creature_skills AS skill
                    WHERE skill.creature_id = creature.id AND skill.skill = 'Pickpocketing'
                );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "items", table: "crimes");

            migrationBuilder.DropColumn(name: "outcome", table: "crimes");

            migrationBuilder.DropColumn(name: "owner_creature_id", table: "crimes");

            migrationBuilder.DropColumn(name: "owner_faction_id", table: "crimes");

            migrationBuilder.DropColumn(name: "owner_name", table: "crimes");

            migrationBuilder.DropColumn(name: "source_owner_id", table: "crimes");

            migrationBuilder.DropColumn(name: "source_owner_type", table: "crimes");

            migrationBuilder.Sql("DELETE FROM creature_skills WHERE skill = 'Pickpocketing';");
        }
    }
}
