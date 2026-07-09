using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomConnectorKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "room_connector_key_item_id", table: "props");

            migrationBuilder.CreateTable(
                name: "room_connector_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_connector_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_connector_keys", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_connector_keys_item_id",
                table: "room_connector_keys",
                column: "item_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_connector_keys_room_connector_id",
                table: "room_connector_keys",
                column: "room_connector_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "room_connector_keys");

            migrationBuilder.AddColumn<Guid>(
                name: "room_connector_key_item_id",
                table: "props",
                type: "uuid",
                nullable: true
            );
        }
    }
}
