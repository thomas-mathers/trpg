using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "building_type",
                table: "buildings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "building_type",
                table: "buildings");
        }
    }
}
