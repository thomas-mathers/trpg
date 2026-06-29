using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class FlattenProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "progression",
                table: "persons");

            migrationBuilder.AddColumn<int>(
                name: "experience",
                table: "persons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "level",
                table: "persons",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "experience",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "level",
                table: "persons");

            migrationBuilder.AddColumn<string>(
                name: "progression",
                table: "persons",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");
        }
    }
}
