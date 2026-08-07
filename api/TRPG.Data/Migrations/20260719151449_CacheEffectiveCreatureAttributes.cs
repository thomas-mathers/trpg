using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class CacheEffectiveCreatureAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "attributes",
                table: "creatures",
                newName: "base_attributes"
            );

            migrationBuilder.AddColumn<int>(
                name: "defense",
                table: "creatures",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "dexterity",
                table: "creatures",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "endurance",
                table: "creatures",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<float>(
                name: "fire_resistance",
                table: "creatures",
                type: "real",
                nullable: false,
                defaultValue: 0f
            );

            migrationBuilder.AddColumn<float>(
                name: "ice_resistance",
                table: "creatures",
                type: "real",
                nullable: false,
                defaultValue: 0f
            );

            migrationBuilder.AddColumn<int>(
                name: "intelligence",
                table: "creatures",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<float>(
                name: "lightning_resistance",
                table: "creatures",
                type: "real",
                nullable: false,
                defaultValue: 0f
            );

            migrationBuilder.AddColumn<float>(
                name: "magic_resistance",
                table: "creatures",
                type: "real",
                nullable: false,
                defaultValue: 0f
            );

            migrationBuilder.AddColumn<int>(
                name: "mana",
                table: "creatures",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "maximum_ap",
                table: "creatures",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "maximum_hp",
                table: "creatures",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "maximum_mp",
                table: "creatures",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<float>(
                name: "movement_speed",
                table: "creatures",
                type: "real",
                nullable: false,
                defaultValue: 0f
            );

            migrationBuilder.AddColumn<float>(
                name: "physical_resistance",
                table: "creatures",
                type: "real",
                nullable: false,
                defaultValue: 0f
            );

            migrationBuilder.AddColumn<float>(
                name: "poison_resistance",
                table: "creatures",
                type: "real",
                nullable: false,
                defaultValue: 0f
            );

            migrationBuilder.AddColumn<int>(
                name: "stamina",
                table: "creatures",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "strength",
                table: "creatures",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "defense", table: "creatures");

            migrationBuilder.DropColumn(name: "dexterity", table: "creatures");

            migrationBuilder.DropColumn(name: "endurance", table: "creatures");

            migrationBuilder.DropColumn(name: "fire_resistance", table: "creatures");

            migrationBuilder.DropColumn(name: "ice_resistance", table: "creatures");

            migrationBuilder.DropColumn(name: "intelligence", table: "creatures");

            migrationBuilder.DropColumn(name: "lightning_resistance", table: "creatures");

            migrationBuilder.DropColumn(name: "magic_resistance", table: "creatures");

            migrationBuilder.DropColumn(name: "mana", table: "creatures");

            migrationBuilder.DropColumn(name: "maximum_ap", table: "creatures");

            migrationBuilder.DropColumn(name: "maximum_hp", table: "creatures");

            migrationBuilder.DropColumn(name: "maximum_mp", table: "creatures");

            migrationBuilder.DropColumn(name: "movement_speed", table: "creatures");

            migrationBuilder.DropColumn(name: "physical_resistance", table: "creatures");

            migrationBuilder.DropColumn(name: "poison_resistance", table: "creatures");

            migrationBuilder.DropColumn(name: "stamina", table: "creatures");

            migrationBuilder.DropColumn(name: "strength", table: "creatures");

            migrationBuilder.RenameColumn(
                name: "base_attributes",
                table: "creatures",
                newName: "attributes"
            );
        }
    }
}
