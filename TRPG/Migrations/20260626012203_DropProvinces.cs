using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class DropProvinces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provinces");

            migrationBuilder.RenameColumn(
                name: "province_id",
                table: "cities",
                newName: "country_id");

            migrationBuilder.RenameIndex(
                name: "ix_cities_province_id_name",
                table: "cities",
                newName: "ix_cities_country_id_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "country_id",
                table: "cities",
                newName: "province_id");

            migrationBuilder.RenameIndex(
                name: "ix_cities_country_id_name",
                table: "cities",
                newName: "ix_cities_province_id_name");

            migrationBuilder.CreateTable(
                name: "provinces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    boundary = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provinces", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_provinces_country_id_name",
                table: "provinces",
                columns: new[] { "country_id", "name" },
                unique: true);
        }
    }
}
