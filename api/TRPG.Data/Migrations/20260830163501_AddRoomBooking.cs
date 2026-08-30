using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "room_bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_at_playtime = table.Column<TimeSpan>(type: "interval", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_bookings", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_bookings_player_id",
                table: "room_bookings",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_bookings_room_id",
                table: "room_bookings",
                column: "room_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_room_bookings_world_id",
                table: "room_bookings",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "room_bookings");
        }
    }
}
