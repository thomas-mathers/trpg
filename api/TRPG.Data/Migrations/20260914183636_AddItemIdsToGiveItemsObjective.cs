using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddItemIdsToGiveItemsObjective : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "item_ids",
                table: "quest_objectives",
                type: "uuid[]",
                nullable: true
            );

            // GiveItemObjective.ItemId (the shared "item_id" column) becomes
            // GiveItemsObjective.ItemIds — carry each existing single id into the new array
            // column before the scalar column stops being populated for this discriminator.
            migrationBuilder.Sql(
                "UPDATE quest_objectives SET item_ids = ARRAY[item_id] "
                    + "WHERE objective_kind = 'GiveItem' AND item_id IS NOT NULL;"
            );

            // DeliverItemObjective.ItemId no longer collides with GiveItemObjective.ItemId now
            // that the latter is gone, so EF reclaims the shared "item_id" column for it instead
            // of its own deliver_item_objective_item_id column — migrate existing values across
            // before dropping that column, or every in-progress delivery quest loses its item.
            migrationBuilder.Sql(
                "UPDATE quest_objectives SET item_id = deliver_item_objective_item_id "
                    + "WHERE objective_kind = 'DeliverItem' AND deliver_item_objective_item_id IS NOT NULL;"
            );

            migrationBuilder.DropColumn(
                name: "deliver_item_objective_item_id",
                table: "quest_objectives"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "deliver_item_objective_item_id",
                table: "quest_objectives",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.Sql(
                "UPDATE quest_objectives SET deliver_item_objective_item_id = item_id "
                    + "WHERE objective_kind = 'DeliverItem' AND item_id IS NOT NULL;"
            );

            migrationBuilder.Sql(
                "UPDATE quest_objectives SET item_id = item_ids[1] "
                    + "WHERE objective_kind = 'GiveItem' AND coalesce(array_length(item_ids, 1), 0) > 0;"
            );

            migrationBuilder.DropColumn(name: "item_ids", table: "quest_objectives");
        }
    }
}
