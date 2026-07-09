using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class ItemLevelsAndModifierHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "modifiers",
                table: "items",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "item_level",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "required_level",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "item_level", table: "items");

            migrationBuilder.DropColumn(name: "required_level", table: "items");

            migrationBuilder.AlterColumn<string>(
                name: "modifiers",
                table: "items",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb"
            );
        }
    }
}
