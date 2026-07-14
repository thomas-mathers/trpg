using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class DropCreatureActiveConditionsAndModifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "active_conditions", table: "creatures");

            migrationBuilder.DropColumn(name: "active_modifiers", table: "creatures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "active_conditions",
                table: "creatures",
                type: "jsonb",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "active_modifiers",
                table: "creatures",
                type: "jsonb",
                nullable: true
            );
        }
    }
}
