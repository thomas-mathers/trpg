using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameQuestItemToCanTradeAndAddItemIsHidden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "is_quest_item",
                table: "items",
                newName: "can_trade"
            );

            migrationBuilder.Sql("UPDATE items SET can_trade = NOT can_trade;");

            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                table: "items",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "is_hidden", table: "items");

            migrationBuilder.Sql("UPDATE items SET can_trade = NOT can_trade;");

            migrationBuilder.RenameColumn(
                name: "can_trade",
                table: "items",
                newName: "is_quest_item"
            );
        }
    }
}
