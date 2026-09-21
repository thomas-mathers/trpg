using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaravanDirectionAndFractionalLinger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "direction",
                table: "caravans",
                type: "text",
                nullable: false,
                defaultValue: "Clockwise"
            );

            migrationBuilder.AlterColumn<double>(
                name: "linger_hours",
                table: "caravan_routes",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "direction", table: "caravans");

            migrationBuilder.AlterColumn<int>(
                name: "linger_hours",
                table: "caravan_routes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision"
            );
        }
    }
}
