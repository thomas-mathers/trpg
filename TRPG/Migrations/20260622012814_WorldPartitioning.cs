using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class WorldPartitioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_skills_name", table: "skills");

            migrationBuilder.DropIndex(name: "ix_races_name", table: "races");

            migrationBuilder.DropIndex(name: "ix_professions_name", table: "professions");

            migrationBuilder.DropIndex(
                name: "ix_persons_location_world_id_location_city_id_location_buildin",
                table: "persons"
            );

            migrationBuilder.DropIndex(
                name: "ix_jobs_location_world_id_location_city_id_location_building_id",
                table: "jobs"
            );

            migrationBuilder.DropIndex(name: "ix_items_name", table: "items");

            migrationBuilder.DropIndex(name: "ix_factions_name", table: "factions");

            migrationBuilder.DropIndex(name: "ix_effects_name", table: "effects");

            migrationBuilder.DropColumn(name: "location_world_id", table: "jobs");

            migrationBuilder.RenameColumn(
                name: "location_world_id",
                table: "persons",
                newName: "world_id"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "skills",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "races",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "quests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "professions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "npc_conversations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "factions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "effects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.CreateIndex(
                name: "ix_skills_world_id_name",
                table: "skills",
                columns: new[] { "world_id", "name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_races_world_id_name",
                table: "races",
                columns: new[] { "world_id", "name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_quests_world_id",
                table: "quests",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_professions_world_id_name",
                table: "professions",
                columns: new[] { "world_id", "name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_persons_location_city_id_location_building_id",
                table: "persons",
                columns: new[] { "location_city_id", "location_building_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_persons_world_id",
                table: "persons",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_npc_conversations_world_id",
                table: "npc_conversations",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_jobs_location_city_id_location_building_id",
                table: "jobs",
                columns: new[] { "location_city_id", "location_building_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_items_world_id_name",
                table: "items",
                columns: new[] { "world_id", "name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_factions_world_id_name",
                table: "factions",
                columns: new[] { "world_id", "name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_effects_world_id_name",
                table: "effects",
                columns: new[] { "world_id", "name" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_skills_world_id_name", table: "skills");

            migrationBuilder.DropIndex(name: "ix_races_world_id_name", table: "races");

            migrationBuilder.DropIndex(name: "ix_quests_world_id", table: "quests");

            migrationBuilder.DropIndex(name: "ix_professions_world_id_name", table: "professions");

            migrationBuilder.DropIndex(
                name: "ix_persons_location_city_id_location_building_id",
                table: "persons"
            );

            migrationBuilder.DropIndex(name: "ix_persons_world_id", table: "persons");

            migrationBuilder.DropIndex(
                name: "ix_npc_conversations_world_id",
                table: "npc_conversations"
            );

            migrationBuilder.DropIndex(
                name: "ix_jobs_location_city_id_location_building_id",
                table: "jobs"
            );

            migrationBuilder.DropIndex(name: "ix_items_world_id_name", table: "items");

            migrationBuilder.DropIndex(name: "ix_factions_world_id_name", table: "factions");

            migrationBuilder.DropIndex(name: "ix_effects_world_id_name", table: "effects");

            migrationBuilder.DropColumn(name: "world_id", table: "skills");

            migrationBuilder.DropColumn(name: "world_id", table: "races");

            migrationBuilder.DropColumn(name: "world_id", table: "quests");

            migrationBuilder.DropColumn(name: "world_id", table: "professions");

            migrationBuilder.DropColumn(name: "world_id", table: "npc_conversations");

            migrationBuilder.DropColumn(name: "world_id", table: "items");

            migrationBuilder.DropColumn(name: "world_id", table: "factions");

            migrationBuilder.DropColumn(name: "world_id", table: "effects");

            migrationBuilder.RenameColumn(
                name: "world_id",
                table: "persons",
                newName: "location_world_id"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "location_world_id",
                table: "jobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.CreateIndex(
                name: "ix_skills_name",
                table: "skills",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_races_name",
                table: "races",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_professions_name",
                table: "professions",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_persons_location_world_id_location_city_id_location_buildin",
                table: "persons",
                columns: new[] { "location_world_id", "location_city_id", "location_building_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_jobs_location_world_id_location_city_id_location_building_id",
                table: "jobs",
                columns: new[] { "location_world_id", "location_city_id", "location_building_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_items_name",
                table: "items",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_factions_name",
                table: "factions",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_effects_name",
                table: "effects",
                column: "name",
                unique: true
            );
        }
    }
}
