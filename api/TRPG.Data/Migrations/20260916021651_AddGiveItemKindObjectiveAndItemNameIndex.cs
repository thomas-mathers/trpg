using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGiveItemKindObjectiveAndItemNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "give_item_kind_objective_recipient_id",
                table: "quest_objectives",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "item_name",
                table: "quest_objectives",
                type: "text",
                nullable: true
            );

            migrationBuilder.CreateIndex(name: "ix_items_name", table: "items", column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_items_name", table: "items");

            migrationBuilder.DropColumn(
                name: "give_item_kind_objective_recipient_id",
                table: "quest_objectives"
            );

            migrationBuilder.DropColumn(name: "item_name", table: "quest_objectives");
        }
    }
}
