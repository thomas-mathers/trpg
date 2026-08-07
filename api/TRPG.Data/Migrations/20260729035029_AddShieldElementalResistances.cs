using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShieldElementalResistances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "fire_resistance",
                table: "items",
                type: "real",
                nullable: true
            );

            migrationBuilder.AddColumn<float>(
                name: "ice_resistance",
                table: "items",
                type: "real",
                nullable: true
            );

            migrationBuilder.AddColumn<float>(
                name: "lightning_resistance",
                table: "items",
                type: "real",
                nullable: true
            );

            migrationBuilder.AddColumn<float>(
                name: "poison_resistance",
                table: "items",
                type: "real",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "fire_resistance", table: "items");

            migrationBuilder.DropColumn(name: "ice_resistance", table: "items");

            migrationBuilder.DropColumn(name: "lightning_resistance", table: "items");

            migrationBuilder.DropColumn(name: "poison_resistance", table: "items");
        }
    }
}
