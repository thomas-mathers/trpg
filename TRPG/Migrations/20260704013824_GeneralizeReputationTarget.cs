using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeReputationTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reputations_person_id_faction_id",
                table: "reputations");

            migrationBuilder.RenameColumn(
                name: "faction_id",
                table: "reputations",
                newName: "target_id");

            migrationBuilder.AddColumn<string>(
                name: "target_type",
                table: "reputations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_reputations_person_id_target_id_target_type",
                table: "reputations",
                columns: new[] { "person_id", "target_id", "target_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reputations_person_id_target_id_target_type",
                table: "reputations");

            migrationBuilder.DropColumn(
                name: "target_type",
                table: "reputations");

            migrationBuilder.RenameColumn(
                name: "target_id",
                table: "reputations",
                newName: "faction_id");

            migrationBuilder.CreateIndex(
                name: "ix_reputations_person_id_faction_id",
                table: "reputations",
                columns: new[] { "person_id", "faction_id" },
                unique: true);
        }
    }
}
