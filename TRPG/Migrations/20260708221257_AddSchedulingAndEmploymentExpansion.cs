using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingAndEmploymentExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "daily", table: "jobs");

            migrationBuilder.AddColumn<string>(
                name: "specific_day",
                table: "jobs",
                type: "text",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "specific_day", table: "jobs");

            migrationBuilder.AddColumn<bool>(
                name: "daily",
                table: "jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }
    }
}
