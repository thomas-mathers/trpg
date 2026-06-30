using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class ItemHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "storage_item_categories",
                table: "props");

            migrationBuilder.DropColumn(
                name: "item_level",
                table: "items");

            migrationBuilder.RenameColumn(
                name: "required_level",
                table: "items",
                newName: "level");

            migrationBuilder.RenameColumn(
                name: "category",
                table: "items",
                newName: "rarity");

            migrationBuilder.AddColumn<string>(
                name: "accessory_type",
                table: "items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ammo_type",
                table: "items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "amount",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "armor_type",
                table: "items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attribute",
                table: "items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "block_chance",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "defense",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "durability_current",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "durability_max",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "duration",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "item_type",
                table: "items",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "max_damage",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "min_damage",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "range",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "shield_item_defense",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "shield_item_durability_current",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "shield_item_durability_max",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "weapon_item_durability_current",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "weapon_item_durability_max",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "weapon_type",
                table: "items",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accessory_type",
                table: "items");

            migrationBuilder.DropColumn(
                name: "ammo_type",
                table: "items");

            migrationBuilder.DropColumn(
                name: "amount",
                table: "items");

            migrationBuilder.DropColumn(
                name: "armor_type",
                table: "items");

            migrationBuilder.DropColumn(
                name: "attribute",
                table: "items");

            migrationBuilder.DropColumn(
                name: "block_chance",
                table: "items");

            migrationBuilder.DropColumn(
                name: "defense",
                table: "items");

            migrationBuilder.DropColumn(
                name: "durability_current",
                table: "items");

            migrationBuilder.DropColumn(
                name: "durability_max",
                table: "items");

            migrationBuilder.DropColumn(
                name: "duration",
                table: "items");

            migrationBuilder.DropColumn(
                name: "item_type",
                table: "items");

            migrationBuilder.DropColumn(
                name: "max_damage",
                table: "items");

            migrationBuilder.DropColumn(
                name: "min_damage",
                table: "items");

            migrationBuilder.DropColumn(
                name: "range",
                table: "items");

            migrationBuilder.DropColumn(
                name: "shield_item_defense",
                table: "items");

            migrationBuilder.DropColumn(
                name: "shield_item_durability_current",
                table: "items");

            migrationBuilder.DropColumn(
                name: "shield_item_durability_max",
                table: "items");

            migrationBuilder.DropColumn(
                name: "weapon_item_durability_current",
                table: "items");

            migrationBuilder.DropColumn(
                name: "weapon_item_durability_max",
                table: "items");

            migrationBuilder.DropColumn(
                name: "weapon_type",
                table: "items");

            migrationBuilder.RenameColumn(
                name: "rarity",
                table: "items",
                newName: "category");

            migrationBuilder.RenameColumn(
                name: "level",
                table: "items",
                newName: "required_level");

            migrationBuilder.AddColumn<string>(
                name: "storage_item_categories",
                table: "props",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "item_level",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
