using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class DropBuildingNameUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_buildings_region_id_name", table: "buildings");

            migrationBuilder.CreateIndex(
                name: "ix_buildings_region_id_name",
                table: "buildings",
                columns: new[] { "region_id", "name" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_buildings_region_id_name", table: "buildings");

            migrationBuilder.CreateIndex(
                name: "ix_buildings_region_id_name",
                table: "buildings",
                columns: new[] { "region_id", "name" },
                unique: true
            );
        }
    }
}
