using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameLeverAndTriggerProps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "is_pulled",
                table: "props",
                newName: "is_activated"
            );

            migrationBuilder.RenameColumn(
                name: "trigger_id",
                table: "encounters",
                newName: "trap_id"
            );

            // The Prop TPH discriminator's string values are runtime data, not schema shape, so EF's
            // migration diff never generates this by itself. Trap rows (formerly discriminated as
            // "Trigger") must be relabeled before Trigger is reused below, or both meanings would
            // briefly collide under one value.
            migrationBuilder.Sql(
                "UPDATE props SET behavior_type = 'Trap' WHERE behavior_type = 'Trigger';"
            );
            migrationBuilder.Sql(
                "UPDATE props SET behavior_type = 'Trigger' WHERE behavior_type = 'Lever';"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE props SET behavior_type = 'Lever' WHERE behavior_type = 'Trigger';"
            );
            migrationBuilder.Sql(
                "UPDATE props SET behavior_type = 'Trigger' WHERE behavior_type = 'Trap';"
            );

            migrationBuilder.RenameColumn(
                name: "is_activated",
                table: "props",
                newName: "is_pulled"
            );

            migrationBuilder.RenameColumn(
                name: "trap_id",
                table: "encounters",
                newName: "trigger_id"
            );
        }
    }
}
