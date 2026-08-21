using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrimeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crime_witnesses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    crime_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolution = table.Column<string>(type: "text", nullable: false),
                    resolved_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    witnessed_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_crime_witnesses", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "crimes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolution = table.Column<string>(type: "text", nullable: false),
                    resolved_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    occurred_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    crime_type = table.Column<string>(
                        type: "character varying(5)",
                        maxLength: 5,
                        nullable: false
                    ),
                    victim_id = table.Column<Guid>(type: "uuid", nullable: true),
                    victim_name = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_crimes", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_crime_witnesses_crime_id_creature_id",
                table: "crime_witnesses",
                columns: new[] { "crime_id", "creature_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_crime_witnesses_world_id",
                table: "crime_witnesses",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_crime_witnesses_world_id_creature_id",
                table: "crime_witnesses",
                columns: new[] { "world_id", "creature_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_crimes_world_id",
                table: "crimes",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_crimes_world_id_player_id_location_id",
                table: "crimes",
                columns: new[] { "world_id", "player_id", "location_id" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "crime_witnesses");

            migrationBuilder.DropTable(name: "crimes");
        }
    }
}
