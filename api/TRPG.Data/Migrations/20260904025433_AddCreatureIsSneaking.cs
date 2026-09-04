using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureIsSneaking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_sneaking",
                table: "creatures",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "is_sneaking", table: "creatures");
        }
    }
}
