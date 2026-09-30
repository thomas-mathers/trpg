using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFightEncounterPlayerWasAggressor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "player_was_aggressor",
                table: "encounters",
                type: "boolean",
                nullable: true
            );

            migrationBuilder.Sql(
                "UPDATE encounters SET player_was_aggressor = TRUE WHERE encounter_type = 'Fight'"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "player_was_aggressor", table: "encounters");
        }
    }
}
