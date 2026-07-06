using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceRaceWithCreatureType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "races");

            migrationBuilder.DropColumn(
                name: "race_id",
                table: "creatures");

            migrationBuilder.AlterColumn<string>(
                name: "profession",
                table: "creatures",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "creature_type",
                table: "creatures",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "creature_type",
                table: "creatures");

            migrationBuilder.AlterColumn<string>(
                name: "profession",
                table: "creatures",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "race_id",
                table: "creatures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "races",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    culture_style = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_races", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_races_world_id_name",
                table: "races",
                columns: new[] { "world_id", "name" },
                unique: true);
        }
    }
}
