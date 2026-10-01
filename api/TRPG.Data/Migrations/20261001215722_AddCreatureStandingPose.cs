using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureStandingPose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "standing_angle",
                table: "creatures",
                type: "double precision",
                nullable: true
            );

            migrationBuilder.AddColumn<double>(
                name: "standing_x",
                table: "creatures",
                type: "double precision",
                nullable: true
            );

            migrationBuilder.AddColumn<double>(
                name: "standing_y",
                table: "creatures",
                type: "double precision",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "standing_angle", table: "creatures");

            migrationBuilder.DropColumn(name: "standing_x", table: "creatures");

            migrationBuilder.DropColumn(name: "standing_y", table: "creatures");
        }
    }
}
