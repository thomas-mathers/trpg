using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class SkillRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "effects");

            migrationBuilder.DropColumn(name: "active_effect_ids", table: "skills");

            migrationBuilder.DropColumn(name: "passive_effect_ids", table: "skills");

            migrationBuilder.DropColumn(name: "active_effect_ids", table: "items");

            migrationBuilder.DropColumn(name: "passive_effect_ids", table: "items");

            migrationBuilder.RenameColumn(name: "ap_cost", table: "skills", newName: "cost");

            migrationBuilder.RenameColumn(
                name: "cooldown_turns",
                table: "skills",
                newName: "cooldown"
            );

            migrationBuilder.AddColumn<string>(
                name: "conditions",
                table: "skills",
                type: "jsonb",
                nullable: true
            );

            migrationBuilder.AddColumn<float>(
                name: "damage_amount",
                table: "skills",
                type: "real",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "damage_amount_type",
                table: "skills",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "damage_type",
                table: "skills",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "duration",
                table: "skills",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "modifiers",
                table: "skills",
                type: "jsonb",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "skill_type",
                table: "skills",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "active_conditions",
                table: "persons",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb"
            );

            migrationBuilder.AddColumn<string>(
                name: "active_modifiers",
                table: "persons",
                type: "jsonb",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "modifiers",
                table: "items",
                type: "jsonb",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "conditions", table: "skills");

            migrationBuilder.DropColumn(name: "damage_amount", table: "skills");

            migrationBuilder.DropColumn(name: "damage_amount_type", table: "skills");

            migrationBuilder.DropColumn(name: "damage_type", table: "skills");

            migrationBuilder.DropColumn(name: "duration", table: "skills");

            migrationBuilder.DropColumn(name: "modifiers", table: "skills");

            migrationBuilder.DropColumn(name: "skill_type", table: "skills");

            migrationBuilder.DropColumn(name: "active_conditions", table: "persons");

            migrationBuilder.DropColumn(name: "active_modifiers", table: "persons");

            migrationBuilder.DropColumn(name: "modifiers", table: "items");

            migrationBuilder.RenameColumn(name: "cost", table: "skills", newName: "ap_cost");

            migrationBuilder.RenameColumn(
                name: "cooldown",
                table: "skills",
                newName: "cooldown_turns"
            );

            migrationBuilder.AddColumn<List<Guid>>(
                name: "active_effect_ids",
                table: "skills",
                type: "uuid[]",
                nullable: false
            );

            migrationBuilder.AddColumn<List<Guid>>(
                name: "passive_effect_ids",
                table: "skills",
                type: "uuid[]",
                nullable: false
            );

            migrationBuilder.AddColumn<List<Guid>>(
                name: "active_effect_ids",
                table: "items",
                type: "uuid[]",
                nullable: false
            );

            migrationBuilder.AddColumn<List<Guid>>(
                name: "passive_effect_ids",
                table: "items",
                type: "uuid[]",
                nullable: false
            );

            migrationBuilder.CreateTable(
                name: "effects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_mode = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    duration = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    stat = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<float>(type: "real", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_effects", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_effects_world_id_name",
                table: "effects",
                columns: new[] { "world_id", "name" },
                unique: true
            );
        }
    }
}
