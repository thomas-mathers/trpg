using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomConnectorIsLocked : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_locked",
                table: "props",
                type: "boolean",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "is_locked", table: "props");
        }
    }
}
