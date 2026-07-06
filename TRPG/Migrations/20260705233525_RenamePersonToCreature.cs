using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class RenamePersonToCreature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "person_abilities");

            migrationBuilder.DropTable(
                name: "person_quest_objectives");

            migrationBuilder.DropTable(
                name: "person_quests");

            migrationBuilder.DropTable(
                name: "person_skills");

            migrationBuilder.DropTable(
                name: "persons");

            migrationBuilder.RenameColumn(
                name: "person_id",
                table: "reputations",
                newName: "creature_id");

            migrationBuilder.RenameIndex(
                name: "ix_reputations_person_id_target_id_target_type",
                table: "reputations",
                newName: "ix_reputations_creature_id_target_id_target_type");

            migrationBuilder.RenameColumn(
                name: "workstation_assigned_person_id",
                table: "props",
                newName: "workstation_assigned_creature_id");

            migrationBuilder.RenameColumn(
                name: "assigned_person_id",
                table: "props",
                newName: "assigned_creature_id");

            migrationBuilder.RenameColumn(
                name: "person_id",
                table: "npc_conversations",
                newName: "creature_id");

            migrationBuilder.RenameIndex(
                name: "ix_npc_conversations_npc_id_person_id",
                table: "npc_conversations",
                newName: "ix_npc_conversations_npc_id_creature_id");

            migrationBuilder.RenameColumn(
                name: "person_id",
                table: "jobs",
                newName: "creature_id");

            migrationBuilder.RenameIndex(
                name: "ix_jobs_person_id",
                table: "jobs",
                newName: "ix_jobs_creature_id");

            migrationBuilder.RenameColumn(
                name: "person_id",
                table: "inventory_items",
                newName: "creature_id");

            migrationBuilder.RenameIndex(
                name: "ix_inventory_items_person_id",
                table: "inventory_items",
                newName: "ix_inventory_items_creature_id");

            migrationBuilder.RenameColumn(
                name: "person_id",
                table: "faction_members",
                newName: "creature_id");

            migrationBuilder.RenameIndex(
                name: "ix_faction_members_person_id_faction_id",
                table: "faction_members",
                newName: "ix_faction_members_creature_id_faction_id");

            migrationBuilder.CreateTable(
                name: "creature_abilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ability_name = table.Column<string>(type: "text", nullable: false),
                    cooldown = table.Column<int>(type: "integer", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creature_abilities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "creature_quest_objectives",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    objective_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creature_quest_objectives", x => x.id);
                    table.ForeignKey(
                        name: "fk_creature_quest_objectives_quest_objectives_objective_id",
                        column: x => x.objective_id,
                        principalTable: "quest_objectives",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creature_quests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creature_quests", x => x.id);
                    table.ForeignKey(
                        name: "fk_creature_quests_quests_quest_id",
                        column: x => x.quest_id,
                        principalTable: "quests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creature_skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    experience = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    skill = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creature_skills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "creatures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_conditions = table.Column<string>(type: "jsonb", nullable: false),
                    biography = table.Column<string>(type: "text", nullable: false),
                    birth_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    birth_year = table.Column<int>(type: "integer", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    district_id = table.Column<Guid>(type: "uuid", nullable: true),
                    experience = table.Column<int>(type: "integer", nullable: false),
                    gold = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    profession = table.Column<string>(type: "text", nullable: false),
                    race_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_modifiers = table.Column<string>(type: "jsonb", nullable: true),
                    attributes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creatures", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_creature_abilities_creature_id_ability_name",
                table: "creature_abilities",
                columns: new[] { "creature_id", "ability_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creature_abilities_world_id",
                table: "creature_abilities",
                column: "world_id");

            migrationBuilder.CreateIndex(
                name: "ix_creature_quest_objectives_creature_id_objective_id",
                table: "creature_quest_objectives",
                columns: new[] { "creature_id", "objective_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creature_quest_objectives_objective_id",
                table: "creature_quest_objectives",
                column: "objective_id");

            migrationBuilder.CreateIndex(
                name: "ix_creature_quest_objectives_world_id",
                table: "creature_quest_objectives",
                column: "world_id");

            migrationBuilder.CreateIndex(
                name: "ix_creature_quests_creature_id_quest_id",
                table: "creature_quests",
                columns: new[] { "creature_id", "quest_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creature_quests_quest_id",
                table: "creature_quests",
                column: "quest_id");

            migrationBuilder.CreateIndex(
                name: "ix_creature_quests_world_id",
                table: "creature_quests",
                column: "world_id");

            migrationBuilder.CreateIndex(
                name: "ix_creature_skills_creature_id_skill",
                table: "creature_skills",
                columns: new[] { "creature_id", "skill" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creature_skills_world_id",
                table: "creature_skills",
                column: "world_id");

            migrationBuilder.CreateIndex(
                name: "ix_creatures_state_id_room_id",
                table: "creatures",
                columns: new[] { "state_id", "room_id" });

            migrationBuilder.CreateIndex(
                name: "ix_creatures_world_id",
                table: "creatures",
                column: "world_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creature_abilities");

            migrationBuilder.DropTable(
                name: "creature_quest_objectives");

            migrationBuilder.DropTable(
                name: "creature_quests");

            migrationBuilder.DropTable(
                name: "creature_skills");

            migrationBuilder.DropTable(
                name: "creatures");

            migrationBuilder.RenameColumn(
                name: "creature_id",
                table: "reputations",
                newName: "person_id");

            migrationBuilder.RenameIndex(
                name: "ix_reputations_creature_id_target_id_target_type",
                table: "reputations",
                newName: "ix_reputations_person_id_target_id_target_type");

            migrationBuilder.RenameColumn(
                name: "workstation_assigned_creature_id",
                table: "props",
                newName: "workstation_assigned_person_id");

            migrationBuilder.RenameColumn(
                name: "assigned_creature_id",
                table: "props",
                newName: "assigned_person_id");

            migrationBuilder.RenameColumn(
                name: "creature_id",
                table: "npc_conversations",
                newName: "person_id");

            migrationBuilder.RenameIndex(
                name: "ix_npc_conversations_npc_id_creature_id",
                table: "npc_conversations",
                newName: "ix_npc_conversations_npc_id_person_id");

            migrationBuilder.RenameColumn(
                name: "creature_id",
                table: "jobs",
                newName: "person_id");

            migrationBuilder.RenameIndex(
                name: "ix_jobs_creature_id",
                table: "jobs",
                newName: "ix_jobs_person_id");

            migrationBuilder.RenameColumn(
                name: "creature_id",
                table: "inventory_items",
                newName: "person_id");

            migrationBuilder.RenameIndex(
                name: "ix_inventory_items_creature_id",
                table: "inventory_items",
                newName: "ix_inventory_items_person_id");

            migrationBuilder.RenameColumn(
                name: "creature_id",
                table: "faction_members",
                newName: "person_id");

            migrationBuilder.RenameIndex(
                name: "ix_faction_members_creature_id_faction_id",
                table: "faction_members",
                newName: "ix_faction_members_person_id_faction_id");

            migrationBuilder.CreateTable(
                name: "person_abilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ability_name = table.Column<string>(type: "text", nullable: false),
                    cooldown = table.Column<int>(type: "integer", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_abilities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "person_quest_objectives",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    objective_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_quest_objectives", x => x.id);
                    table.ForeignKey(
                        name: "fk_person_quest_objectives_quest_objectives_objective_id",
                        column: x => x.objective_id,
                        principalTable: "quest_objectives",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_quests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_quests", x => x.id);
                    table.ForeignKey(
                        name: "fk_person_quests_quests_quest_id",
                        column: x => x.quest_id,
                        principalTable: "quests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    experience = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_skills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "persons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_conditions = table.Column<string>(type: "jsonb", nullable: false),
                    biography = table.Column<string>(type: "text", nullable: false),
                    birth_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    birth_year = table.Column<int>(type: "integer", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    district_id = table.Column<Guid>(type: "uuid", nullable: true),
                    experience = table.Column<int>(type: "integer", nullable: false),
                    gold = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    profession = table.Column<string>(type: "text", nullable: false),
                    race_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_modifiers = table.Column<string>(type: "jsonb", nullable: true),
                    attributes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_persons", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_person_abilities_person_id_ability_name",
                table: "person_abilities",
                columns: new[] { "person_id", "ability_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_person_abilities_world_id",
                table: "person_abilities",
                column: "world_id");

            migrationBuilder.CreateIndex(
                name: "ix_person_quest_objectives_objective_id",
                table: "person_quest_objectives",
                column: "objective_id");

            migrationBuilder.CreateIndex(
                name: "ix_person_quest_objectives_person_id_objective_id",
                table: "person_quest_objectives",
                columns: new[] { "person_id", "objective_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_person_quest_objectives_world_id",
                table: "person_quest_objectives",
                column: "world_id");

            migrationBuilder.CreateIndex(
                name: "ix_person_quests_person_id_quest_id",
                table: "person_quests",
                columns: new[] { "person_id", "quest_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_person_quests_quest_id",
                table: "person_quests",
                column: "quest_id");

            migrationBuilder.CreateIndex(
                name: "ix_person_quests_world_id",
                table: "person_quests",
                column: "world_id");

            migrationBuilder.CreateIndex(
                name: "ix_person_skills_person_id_skill",
                table: "person_skills",
                columns: new[] { "person_id", "skill" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_person_skills_world_id",
                table: "person_skills",
                column: "world_id");

            migrationBuilder.CreateIndex(
                name: "ix_persons_state_id_room_id",
                table: "persons",
                columns: new[] { "state_id", "room_id" });

            migrationBuilder.CreateIndex(
                name: "ix_persons_world_id",
                table: "persons",
                column: "world_id");
        }
    }
}
