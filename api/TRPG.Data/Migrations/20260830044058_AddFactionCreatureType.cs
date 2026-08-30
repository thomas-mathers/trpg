using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFactionCreatureType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "creature_type",
                table: "factions",
                type: "text",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_factions_world_id_creature_type",
                table: "factions",
                columns: new[] { "world_id", "creature_type" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_factions_world_id_creature_type",
                table: "factions"
            );

            migrationBuilder.DropColumn(name: "creature_type", table: "factions");
        }
    }
}
