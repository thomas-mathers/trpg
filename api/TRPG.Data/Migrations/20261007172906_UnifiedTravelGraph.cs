using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedTravelGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM location_connectors;");

            migrationBuilder.DropTable(name: "road_edges");

            migrationBuilder.DropTable(name: "road_nodes");

            migrationBuilder.DropTable(name: "travel_connectors");

            migrationBuilder.DropPrimaryKey(
                name: "pk_location_connectors",
                table: "location_connectors"
            );

            migrationBuilder.DropColumn(name: "arrival_x", table: "location_connectors");

            migrationBuilder.DropColumn(name: "arrival_y", table: "location_connectors");

            migrationBuilder.DropColumn(name: "exit_x", table: "location_connectors");

            migrationBuilder.RenameTable(name: "location_connectors", newName: "connectors");

            migrationBuilder.RenameColumn(name: "exit_y", table: "connectors", newName: "distance");

            migrationBuilder.RenameIndex(
                name: "ix_location_connectors_world_id",
                table: "connectors",
                newName: "ix_connectors_world_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_location_connectors_origin_location_id_destination_location",
                table: "connectors",
                newName: "ix_connectors_origin_location_id_destination_location_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_location_connectors_origin_location_id",
                table: "connectors",
                newName: "ix_connectors_origin_location_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_location_connectors_destination_location_id",
                table: "connectors",
                newName: "ix_connectors_destination_location_id"
            );

            migrationBuilder.AddColumn<double>(
                name: "distance",
                table: "route_steps",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "origin_location_id",
                table: "connectors",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid"
            );

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "connectors",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text"
            );

            migrationBuilder.AlterColumn<double>(
                name: "exit_angle",
                table: "connectors",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision"
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "destination_location_id",
                table: "connectors",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid"
            );

            migrationBuilder.AlterColumn<string>(
                name: "destination_label",
                table: "connectors",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text"
            );

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "connectors",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text"
            );

            migrationBuilder.AlterColumn<double>(
                name: "arrival_angle",
                table: "connectors",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision"
            );

            migrationBuilder.AddColumn<bool>(
                name: "bidirectional",
                table: "connectors",
                type: "boolean",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "connector_kind",
                table: "connectors",
                type: "text",
                maxLength: 13,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<Guid>(
                name: "destination_node_id",
                table: "connectors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                table: "connectors",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "origin_node_id",
                table: "connectors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<string>(
                name: "road_class",
                table: "connectors",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "waypoints",
                table: "connectors",
                type: "jsonb",
                nullable: true
            );

            migrationBuilder.AddPrimaryKey(
                name: "pk_connectors",
                table: "connectors",
                column: "id"
            );

            migrationBuilder.CreateTable(
                name: "travel_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_x = table.Column<double>(type: "double precision", nullable: false),
                    position_y = table.Column<double>(type: "double precision", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_travel_nodes", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_connectors_destination_node_id",
                table: "connectors",
                column: "destination_node_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_connectors_location_id",
                table: "connectors",
                column: "location_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_connectors_origin_node_id",
                table: "connectors",
                column: "origin_node_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_travel_nodes_location_id",
                table: "travel_nodes",
                column: "location_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_travel_nodes_world_id",
                table: "travel_nodes",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "travel_nodes");

            migrationBuilder.DropPrimaryKey(name: "pk_connectors", table: "connectors");

            migrationBuilder.DropIndex(
                name: "ix_connectors_destination_node_id",
                table: "connectors"
            );

            migrationBuilder.DropIndex(name: "ix_connectors_location_id", table: "connectors");

            migrationBuilder.DropIndex(name: "ix_connectors_origin_node_id", table: "connectors");

            migrationBuilder.DropColumn(name: "distance", table: "route_steps");

            migrationBuilder.DropColumn(name: "bidirectional", table: "connectors");

            migrationBuilder.DropColumn(name: "connector_kind", table: "connectors");

            migrationBuilder.DropColumn(name: "destination_node_id", table: "connectors");

            migrationBuilder.DropColumn(name: "location_id", table: "connectors");

            migrationBuilder.DropColumn(name: "origin_node_id", table: "connectors");

            migrationBuilder.DropColumn(name: "road_class", table: "connectors");

            migrationBuilder.DropColumn(name: "waypoints", table: "connectors");

            migrationBuilder.RenameTable(name: "connectors", newName: "location_connectors");

            migrationBuilder.RenameColumn(
                name: "distance",
                table: "location_connectors",
                newName: "exit_y"
            );

            migrationBuilder.RenameIndex(
                name: "ix_connectors_world_id",
                table: "location_connectors",
                newName: "ix_location_connectors_world_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_connectors_origin_location_id_destination_location_id",
                table: "location_connectors",
                newName: "ix_location_connectors_origin_location_id_destination_location"
            );

            migrationBuilder.RenameIndex(
                name: "ix_connectors_origin_location_id",
                table: "location_connectors",
                newName: "ix_location_connectors_origin_location_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_connectors_destination_location_id",
                table: "location_connectors",
                newName: "ix_location_connectors_destination_location_id"
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "origin_location_id",
                table: "location_connectors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "location_connectors",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<double>(
                name: "exit_angle",
                table: "location_connectors",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "destination_location_id",
                table: "location_connectors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<string>(
                name: "destination_label",
                table: "location_connectors",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "location_connectors",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<double>(
                name: "arrival_angle",
                table: "location_connectors",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true
            );

            migrationBuilder.AddColumn<double>(
                name: "arrival_x",
                table: "location_connectors",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "arrival_y",
                table: "location_connectors",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "exit_x",
                table: "location_connectors",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddPrimaryKey(
                name: "pk_location_connectors",
                table: "location_connectors",
                column: "id"
            );

            migrationBuilder.CreateTable(
                name: "road_edges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    @class = table.Column<string>(name: "class", type: "text", nullable: false),
                    from_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    length = table.Column<double>(type: "double precision", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    waypoints = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_road_edges", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "road_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x = table.Column<double>(type: "double precision", nullable: false),
                    y = table.Column<double>(type: "double precision", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_road_nodes", x => x.id);
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
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_travel_connectors", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_road_edges_location_id",
                table: "road_edges",
                column: "location_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_road_edges_world_id",
                table: "road_edges",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_road_nodes_connector_id",
                table: "road_nodes",
                column: "connector_id",
                unique: true,
                filter: "connector_id IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "ix_road_nodes_location_id",
                table: "road_nodes",
                column: "location_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_road_nodes_world_id",
                table: "road_nodes",
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
    }
}
