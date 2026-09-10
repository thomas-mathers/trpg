using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeverAndDoorConnectorLever : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_pulled",
                table: "props",
                type: "boolean",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "door_connector_levers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lever_id = table.Column<Guid>(type: "uuid", nullable: false),
                    door_connector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_door_connector_levers", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_door_connector_levers_door_connector_id",
                table: "door_connector_levers",
                column: "door_connector_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_door_connector_levers_lever_id",
                table: "door_connector_levers",
                column: "lever_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_door_connector_levers_world_id",
                table: "door_connector_levers",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "door_connector_levers");

            migrationBuilder.DropColumn(name: "is_pulled", table: "props");
        }
    }
}
