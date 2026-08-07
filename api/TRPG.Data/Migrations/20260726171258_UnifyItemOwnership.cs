using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifyItemOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "container_items");

            migrationBuilder.DropTable(name: "inventory_items");

            migrationBuilder.RenameColumn(
                name: "weapon_item_durability_max",
                table: "items",
                newName: "weapon_durability_max"
            );

            migrationBuilder.RenameColumn(
                name: "weapon_item_durability_current",
                table: "items",
                newName: "weapon_durability_current"
            );

            migrationBuilder.RenameColumn(
                name: "shield_item_durability_max",
                table: "items",
                newName: "shield_durability_max"
            );

            migrationBuilder.RenameColumn(
                name: "shield_item_durability_current",
                table: "items",
                newName: "shield_durability_current"
            );

            migrationBuilder.RenameColumn(
                name: "shield_item_defense",
                table: "items",
                newName: "shield_defense"
            );

            migrationBuilder.RenameColumn(
                name: "amount",
                table: "items",
                newName: "restore_amount"
            );

            migrationBuilder.AlterColumn<string>(
                name: "rarity",
                table: "items",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text"
            );

            migrationBuilder.AlterColumn<int>(
                name: "level",
                table: "items",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer"
            );

            migrationBuilder.AlterColumn<int>(
                name: "gold_value",
                table: "items",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "ownership_acquired_at",
                table: "items",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<string>(
                name: "ownership_equipped_slot",
                table: "items",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "ownership_owner_id",
                table: "items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<string>(
                name: "ownership_owner_type",
                table: "items",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<int>(
                name: "quantity",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.CreateIndex(
                name: "ux_items_gold_owner",
                table: "items",
                column: "ownership_owner_id",
                unique: true,
                filter: "item_type = 'gold'"
            );

            migrationBuilder.CreateIndex(
                name: "ux_items_owner_equipped_slot",
                table: "items",
                columns: new[] { "ownership_owner_id", "ownership_equipped_slot" },
                unique: true,
                filter: "ownership_equipped_slot IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ux_items_gold_owner", table: "items");

            migrationBuilder.DropIndex(name: "ux_items_owner_equipped_slot", table: "items");

            migrationBuilder.DropColumn(name: "ownership_acquired_at", table: "items");

            migrationBuilder.DropColumn(name: "ownership_equipped_slot", table: "items");

            migrationBuilder.DropColumn(name: "ownership_owner_id", table: "items");

            migrationBuilder.DropColumn(name: "ownership_owner_type", table: "items");

            migrationBuilder.DropColumn(name: "quantity", table: "items");

            migrationBuilder.RenameColumn(
                name: "weapon_durability_max",
                table: "items",
                newName: "weapon_item_durability_max"
            );

            migrationBuilder.RenameColumn(
                name: "weapon_durability_current",
                table: "items",
                newName: "weapon_item_durability_current"
            );

            migrationBuilder.RenameColumn(
                name: "shield_durability_max",
                table: "items",
                newName: "shield_item_durability_max"
            );

            migrationBuilder.RenameColumn(
                name: "shield_durability_current",
                table: "items",
                newName: "shield_item_durability_current"
            );

            migrationBuilder.RenameColumn(
                name: "shield_defense",
                table: "items",
                newName: "shield_item_defense"
            );

            migrationBuilder.RenameColumn(
                name: "restore_amount",
                table: "items",
                newName: "amount"
            );

            migrationBuilder.AlterColumn<string>(
                name: "rarity",
                table: "items",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<int>(
                name: "level",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<int>(
                name: "gold_value",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true
            );

            migrationBuilder.CreateTable(
                name: "container_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    container_id = table.Column<Guid>(type: "uuid", nullable: false),
                    index = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_container_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_container_items_props_container_id",
                        column: x => x.container_id,
                        principalTable: "props",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "inventory_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipped_slot = table.Column<string>(type: "text", nullable: true),
                    index = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_items_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_container_items_container_id_index",
                table: "container_items",
                columns: new[] { "container_id", "index" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_container_items_world_id",
                table: "container_items",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_creature_id",
                table: "inventory_items",
                column: "creature_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_item_id",
                table: "inventory_items",
                column: "item_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_world_id",
                table: "inventory_items",
                column: "world_id"
            );
        }
    }
}
