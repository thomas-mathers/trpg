using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldIdToAllEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "states",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "rooms",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "room_connector_keys",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "roads",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "reputations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "quest_objectives",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "props",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "person_skills",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "person_quests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "person_quest_objectives",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "person_abilities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "jobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "inventory_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "faction_members",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "districts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "container_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "cities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "buildings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "world_id",
                table: "building_owners",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.CreateIndex(
                name: "ix_states_world_id",
                table: "states",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_rooms_world_id",
                table: "rooms",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_connector_keys_world_id",
                table: "room_connector_keys",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_roads_world_id",
                table: "roads",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_reputations_world_id",
                table: "reputations",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_quest_objectives_world_id",
                table: "quest_objectives",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_props_world_id",
                table: "props",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_person_skills_world_id",
                table: "person_skills",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_person_quests_world_id",
                table: "person_quests",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_person_quest_objectives_world_id",
                table: "person_quest_objectives",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_person_abilities_world_id",
                table: "person_abilities",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_jobs_world_id",
                table: "jobs",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_items_world_id",
                table: "items",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_world_id",
                table: "inventory_items",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_faction_members_world_id",
                table: "faction_members",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_districts_world_id",
                table: "districts",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_container_items_world_id",
                table: "container_items",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_cities_world_id",
                table: "cities",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_buildings_world_id",
                table: "buildings",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_building_owners_world_id",
                table: "building_owners",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_states_world_id", table: "states");

            migrationBuilder.DropIndex(name: "ix_rooms_world_id", table: "rooms");

            migrationBuilder.DropIndex(
                name: "ix_room_connector_keys_world_id",
                table: "room_connector_keys"
            );

            migrationBuilder.DropIndex(name: "ix_roads_world_id", table: "roads");

            migrationBuilder.DropIndex(name: "ix_reputations_world_id", table: "reputations");

            migrationBuilder.DropIndex(
                name: "ix_quest_objectives_world_id",
                table: "quest_objectives"
            );

            migrationBuilder.DropIndex(name: "ix_props_world_id", table: "props");

            migrationBuilder.DropIndex(name: "ix_person_skills_world_id", table: "person_skills");

            migrationBuilder.DropIndex(name: "ix_person_quests_world_id", table: "person_quests");

            migrationBuilder.DropIndex(
                name: "ix_person_quest_objectives_world_id",
                table: "person_quest_objectives"
            );

            migrationBuilder.DropIndex(
                name: "ix_person_abilities_world_id",
                table: "person_abilities"
            );

            migrationBuilder.DropIndex(name: "ix_jobs_world_id", table: "jobs");

            migrationBuilder.DropIndex(name: "ix_items_world_id", table: "items");

            migrationBuilder.DropIndex(
                name: "ix_inventory_items_world_id",
                table: "inventory_items"
            );

            migrationBuilder.DropIndex(
                name: "ix_faction_members_world_id",
                table: "faction_members"
            );

            migrationBuilder.DropIndex(name: "ix_districts_world_id", table: "districts");

            migrationBuilder.DropIndex(
                name: "ix_container_items_world_id",
                table: "container_items"
            );

            migrationBuilder.DropIndex(name: "ix_cities_world_id", table: "cities");

            migrationBuilder.DropIndex(name: "ix_buildings_world_id", table: "buildings");

            migrationBuilder.DropIndex(
                name: "ix_building_owners_world_id",
                table: "building_owners"
            );

            migrationBuilder.DropColumn(name: "world_id", table: "states");

            migrationBuilder.DropColumn(name: "world_id", table: "rooms");

            migrationBuilder.DropColumn(name: "world_id", table: "room_connector_keys");

            migrationBuilder.DropColumn(name: "world_id", table: "roads");

            migrationBuilder.DropColumn(name: "world_id", table: "reputations");

            migrationBuilder.DropColumn(name: "world_id", table: "quest_objectives");

            migrationBuilder.DropColumn(name: "world_id", table: "props");

            migrationBuilder.DropColumn(name: "world_id", table: "person_skills");

            migrationBuilder.DropColumn(name: "world_id", table: "person_quests");

            migrationBuilder.DropColumn(name: "world_id", table: "person_quest_objectives");

            migrationBuilder.DropColumn(name: "world_id", table: "person_abilities");

            migrationBuilder.DropColumn(name: "world_id", table: "jobs");

            migrationBuilder.DropColumn(name: "world_id", table: "inventory_items");

            migrationBuilder.DropColumn(name: "world_id", table: "faction_members");

            migrationBuilder.DropColumn(name: "world_id", table: "districts");

            migrationBuilder.DropColumn(name: "world_id", table: "container_items");

            migrationBuilder.DropColumn(name: "world_id", table: "cities");

            migrationBuilder.DropColumn(name: "world_id", table: "buildings");

            migrationBuilder.DropColumn(name: "world_id", table: "building_owners");
        }
    }
}
