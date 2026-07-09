using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class AoeAndTargetType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "aoe_radius",
                table: "skills",
                type: "real",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "target_type",
                table: "skills",
                type: "text",
                nullable: false,
                defaultValue: "Single"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "aoe_radius", table: "skills");

            migrationBuilder.DropColumn(name: "target_type", table: "skills");
        }
    }
}
