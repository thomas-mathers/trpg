using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenamePropLocationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(name: "room_id", table: "props", newName: "location_id");

            migrationBuilder.RenameColumn(
                name: "destination_room_id",
                table: "props",
                newName: "destination_location_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_props_room_id",
                table: "props",
                newName: "ix_props_location_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(name: "location_id", table: "props", newName: "room_id");

            migrationBuilder.RenameColumn(
                name: "destination_location_id",
                table: "props",
                newName: "destination_room_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_props_location_id",
                table: "props",
                newName: "ix_props_room_id"
            );
        }
    }
}
