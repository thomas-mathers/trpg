using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "building_owners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_building_owners", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "building_props",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    coordinates = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_building_props", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "buildings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    boundary = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_buildings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    province_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    boundary = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    boundary = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_countries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "effects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    application_mode = table.Column<string>(type: "text", nullable: false),
                    stat = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<float>(type: "real", nullable: false),
                    duration = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_effects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "faction_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    faction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_faction_members", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "factions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_factions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    active_effect_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    passive_effect_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    is_stackable = table.Column<bool>(type: "boolean", nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    gold_value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    start_hour = table.Column<int>(type: "integer", nullable: false),
                    end_hour = table.Column<int>(type: "integer", nullable: false),
                    daily = table.Column<bool>(type: "boolean", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    location_coordinates_x = table.Column<int>(type: "integer", nullable: false),
                    location_coordinates_y = table.Column<int>(type: "integer", nullable: false),
                    location_world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location_building_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "npc_conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    npc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    last_summarized_index = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_npc_conversations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "persons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    biography = table.Column<string>(type: "text", nullable: false),
                    race_id = table.Column<Guid>(type: "uuid", nullable: false),
                    birth_city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    birth_year = table.Column<int>(type: "integer", nullable: false),
                    profession_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_coordinates_x = table.Column<int>(type: "integer", nullable: false),
                    location_coordinates_y = table.Column<int>(type: "integer", nullable: false),
                    location_world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location_building_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gold = table.Column<int>(type: "integer", nullable: false),
                    attributes = table.Column<string>(type: "jsonb", nullable: false),
                    progression = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_persons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "professions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "provinces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    boundary = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provinces", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quest_objectives",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: true),
                    region = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quest_objectives", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    giver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    gold_reward = table.Column<int>(type: "integer", nullable: false),
                    experience_reward = table.Column<int>(type: "integer", nullable: false),
                    item_rewards = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    prerequisite_quest_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "races",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_races", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reputations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    faction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reputations", x => x.id);
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
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    active_effect_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    passive_effect_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    ap_cost = table.Column<int>(type: "integer", nullable: false),
                    cooldown_turns = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "travel_routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    distance = table.Column<float>(type: "real", nullable: false),
                    travel_time = table.Column<int>(type: "integer", nullable: false),
                    danger_level = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_travel_routes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "world_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    region = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "worlds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_worlds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    index = table.Column<int>(type: "integer", nullable: false),
                    equipped_slot = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_items_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "npc_chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    index = table.Column<int>(type: "integer", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_npc_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_npc_chat_messages_npc_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "npc_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_quest_objectives",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    objective_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false)
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
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
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
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cooldown = table.Column<int>(type: "integer", nullable: false)
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
                name: "ix_building_owners_building_id_owner_id",
                table: "building_owners",
                columns: new[] { "building_id", "owner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_building_owners_owner_id",
                table: "building_owners",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_buildings_city_id_name",
                table: "buildings",
                columns: new[] { "city_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cities_province_id_name",
                table: "cities",
                columns: new[] { "province_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_countries_world_id_name",
                table: "countries",
                columns: new[] { "world_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_effects_name",
                table: "effects",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_faction_members_faction_id",
                table: "faction_members",
                column: "faction_id");

            migrationBuilder.CreateIndex(
                name: "ix_faction_members_person_id_faction_id",
                table: "faction_members",
                columns: new[] { "person_id", "faction_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_factions_name",
                table: "factions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_item_id",
                table: "inventory_items",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_person_id",
                table: "inventory_items",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_items_name",
                table: "items",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_jobs_location_world_id_location_city_id_location_building_id",
                table: "jobs",
                columns: new[] { "location_world_id", "location_city_id", "location_building_id" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_person_id",
                table: "jobs",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_npc_chat_messages_conversation_id_index",
                table: "npc_chat_messages",
                columns: new[] { "conversation_id", "index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_npc_conversations_npc_id_person_id",
                table: "npc_conversations",
                columns: new[] { "npc_id", "person_id" },
                unique: true);

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
                name: "ix_person_quests_person_id_quest_id",
                table: "person_quests",
                columns: new[] { "person_id", "quest_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_person_quests_quest_id",
                table: "person_quests",
                column: "quest_id");

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
                name: "ix_persons_location_world_id_location_city_id_location_buildin",
                table: "persons",
                columns: new[] { "location_world_id", "location_city_id", "location_building_id" });

            migrationBuilder.CreateIndex(
                name: "ix_professions_name",
                table: "professions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provinces_country_id_name",
                table: "provinces",
                columns: new[] { "country_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quest_objectives_quest_id",
                table: "quest_objectives",
                column: "quest_id");

            migrationBuilder.CreateIndex(
                name: "ix_quests_giver_id",
                table: "quests",
                column: "giver_id");

            migrationBuilder.CreateIndex(
                name: "ix_races_name",
                table: "races",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reputations_person_id_faction_id",
                table: "reputations",
                columns: new[] { "person_id", "faction_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skills_name",
                table: "skills",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_travel_routes_origin_city_id_destination_city_id",
                table: "travel_routes",
                columns: new[] { "origin_city_id", "destination_city_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_world_events_world_id",
                table: "world_events",
                column: "world_id");

            migrationBuilder.CreateIndex(
                name: "ix_worlds_name",
                table: "worlds",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "building_owners");

            migrationBuilder.DropTable(
                name: "building_props");

            migrationBuilder.DropTable(
                name: "buildings");

            migrationBuilder.DropTable(
                name: "cities");

            migrationBuilder.DropTable(
                name: "countries");

            migrationBuilder.DropTable(
                name: "effects");

            migrationBuilder.DropTable(
                name: "faction_members");

            migrationBuilder.DropTable(
                name: "factions");

            migrationBuilder.DropTable(
                name: "inventory_items");

            migrationBuilder.DropTable(
                name: "jobs");

            migrationBuilder.DropTable(
                name: "npc_chat_messages");

            migrationBuilder.DropTable(
                name: "person_quest_objectives");

            migrationBuilder.DropTable(
                name: "person_quests");

            migrationBuilder.DropTable(
                name: "person_skills");

            migrationBuilder.DropTable(
                name: "persons");

            migrationBuilder.DropTable(
                name: "professions");

            migrationBuilder.DropTable(
                name: "provinces");

            migrationBuilder.DropTable(
                name: "races");

            migrationBuilder.DropTable(
                name: "reputations");

            migrationBuilder.DropTable(
                name: "skill_prerequisites");

            migrationBuilder.DropTable(
                name: "travel_routes");

            migrationBuilder.DropTable(
                name: "world_events");

            migrationBuilder.DropTable(
                name: "worlds");

            migrationBuilder.DropTable(
                name: "items");

            migrationBuilder.DropTable(
                name: "npc_conversations");

            migrationBuilder.DropTable(
                name: "quest_objectives");

            migrationBuilder.DropTable(
                name: "quests");

            migrationBuilder.DropTable(
                name: "skills");
        }
    }
}
