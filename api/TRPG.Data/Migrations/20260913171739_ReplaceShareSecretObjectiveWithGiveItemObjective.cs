using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceShareSecretObjectiveWithGiveItemObjective : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CollectItemObjective.ItemId keeps its existing data by moving off the plain
            // item_id column first, freeing that name for GiveItemObjective.ItemId to reuse.
            migrationBuilder.RenameColumn(
                name: "item_id",
                table: "quest_objectives",
                newName: "collect_item_objective_item_id"
            );

            migrationBuilder.RenameColumn(
                name: "secret_id",
                table: "quest_objectives",
                newName: "item_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "item_id",
                table: "quest_objectives",
                newName: "secret_id"
            );

            migrationBuilder.RenameColumn(
                name: "collect_item_objective_item_id",
                table: "quest_objectives",
                newName: "item_id"
            );
        }
    }
}
