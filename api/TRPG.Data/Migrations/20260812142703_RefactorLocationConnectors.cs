using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorLocationConnectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "location_connector_keys");

            migrationBuilder.DropTable(name: "roads");

            migrationBuilder.DropColumn(name: "destination_label", table: "props");

            migrationBuilder.DropColumn(name: "destination_location_id", table: "props");

            migrationBuilder.DropColumn(name: "destination_type", table: "props");

            migrationBuilder.DropColumn(name: "is_locked", table: "props");

            migrationBuilder.AlterColumn<string>(
                name: "behavior_type",
                table: "props",
                type: "text",
                maxLength: 13,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(21)",
                oldMaxLength: 21
            );

            migrationBuilder.CreateTable(
                name: "door_connector_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    door_connector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_door_connector_keys", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "door_connectors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_door_connectors", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "location_connectors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_label = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    origin_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location_connectors", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "travel_connectors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    danger_level = table.Column<float>(type: "real", nullable: false),
                    distance = table.Column<float>(type: "real", nullable: false),
                    travel_time_hours = table.Column<int>(type: "integer", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_travel_connectors", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_door_connector_keys_door_connector_id",
                table: "door_connector_keys",
                column: "door_connector_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_door_connector_keys_item_id",
                table: "door_connector_keys",
                column: "item_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_door_connector_keys_world_id",
                table: "door_connector_keys",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_door_connectors_connector_id",
                table: "door_connectors",
                column: "connector_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_door_connectors_world_id",
                table: "door_connectors",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_location_connectors_destination_location_id",
                table: "location_connectors",
                column: "destination_location_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_location_connectors_origin_location_id",
                table: "location_connectors",
                column: "origin_location_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_location_connectors_origin_location_id_destination_location",
                table: "location_connectors",
                columns: new[] { "origin_location_id", "destination_location_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_location_connectors_world_id",
                table: "location_connectors",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_travel_connectors_connector_id",
                table: "travel_connectors",
                column: "connector_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_travel_connectors_world_id",
                table: "travel_connectors",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "door_connector_keys");

            migrationBuilder.DropTable(name: "door_connectors");

            migrationBuilder.DropTable(name: "location_connectors");

            migrationBuilder.DropTable(name: "travel_connectors");

            migrationBuilder.AlterColumn<string>(
                name: "behavior_type",
                table: "props",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldMaxLength: 13
            );

            migrationBuilder.AddColumn<string>(
                name: "destination_label",
                table: "props",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "destination_location_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "destination_type",
                table: "props",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "is_locked",
                table: "props",
                type: "boolean",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "location_connector_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_connector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location_connector_keys", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "roads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    danger_level = table.Column<float>(type: "real", nullable: false),
                    destination_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    distance = table.Column<float>(type: "real", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    origin_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    travel_time = table.Column<int>(type: "integer", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roads", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_location_connector_keys_item_id",
                table: "location_connector_keys",
                column: "item_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_location_connector_keys_location_connector_id",
                table: "location_connector_keys",
                column: "location_connector_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_location_connector_keys_world_id",
                table: "location_connector_keys",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_roads_origin_state_id_destination_state_id",
                table: "roads",
                columns: new[] { "origin_state_id", "destination_state_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_roads_world_id",
                table: "roads",
                column: "world_id"
            );
        }
    }
}
