using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceRoomPositionWithMapGeometry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "position", table: "rooms");

            migrationBuilder.AddColumn<string>(
                name: "bounds",
                table: "rooms",
                type: "jsonb",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "path",
                table: "location_connectors",
                type: "jsonb",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "path", table: "location_connectors");

            migrationBuilder.DropColumn(name: "bounds", table: "rooms");

            migrationBuilder.AddColumn<string>(
                name: "position",
                table: "rooms",
                type: "jsonb",
                nullable: true
            );
        }
    }
}
