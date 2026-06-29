using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class RenameSkillsToAbilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "person_skills");

            migrationBuilder.DropTable(
                name: "professions");

            migrationBuilder.DropTable(
                name: "skill_prerequisites");

            migrationBuilder.DropTable(
                name: "skills");

            migrationBuilder.DropColumn(
                name: "profession_id",
                table: "persons");

            migrationBuilder.AddColumn<string>(
                name: "profession",
                table: "persons",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "person_abilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ability_name = table.Column<string>(type: "text", nullable: false),
                    cooldown = table.Column<int>(type: "integer", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_abilities", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_person_abilities_person_id_ability_name",
                table: "person_abilities",
                columns: new[] { "person_id", "ability_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "person_abilities");

            migrationBuilder.DropColumn(
                name: "profession",
                table: "persons");

            migrationBuilder.AddColumn<Guid>(
                name: "profession_id",
                table: "persons",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "professions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "skill_prerequisites",
                columns: table => new
                {
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prerequisite_skill_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skill_prerequisites", x => new { x.skill_id, x.prerequisite_skill_id });
                });

            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aoe_radius = table.Column<float>(type: "real", nullable: true),
                    cooldown = table.Column<int>(type: "integer", nullable: false),
                    cost = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_type = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    damage_amount = table.Column<float>(type: "real", nullable: true),
                    damage_amount_type = table.Column<string>(type: "text", nullable: true),
                    damage_type = table.Column<string>(type: "text", nullable: true),
                    duration = table.Column<int>(type: "integer", nullable: true),
                    conditions = table.Column<string>(type: "jsonb", nullable: true),
                    modifiers = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "person_skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cooldown = table.Column<int>(type: "integer", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_skills", x => x.id);
                    table.ForeignKey(
                        name: "fk_person_skills_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_person_skills_person_id_skill_id",
                table: "person_skills",
                columns: new[] { "person_id", "skill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_person_skills_skill_id",
                table: "person_skills",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_professions_world_id_name",
                table: "professions",
                columns: new[] { "world_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skills_world_id_name",
                table: "skills",
                columns: new[] { "world_id", "name" },
                unique: true);
        }
    }
}
