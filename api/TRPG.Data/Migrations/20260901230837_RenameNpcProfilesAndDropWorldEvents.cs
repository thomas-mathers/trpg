using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameNpcProfilesAndDropWorldEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "world_events");

            migrationBuilder.RenameTable(name: "npc_profiles", newName: "creature_profiles");

            migrationBuilder.Sql(
                "ALTER TABLE creature_profiles RENAME CONSTRAINT pk_npc_profiles TO pk_creature_profiles;"
            );

            migrationBuilder.RenameIndex(
                name: "ix_npc_profiles_creature_id",
                table: "creature_profiles",
                newName: "ix_creature_profiles_creature_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_npc_profiles_world_id",
                table: "creature_profiles",
                newName: "ix_creature_profiles_world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ix_creature_profiles_world_id",
                table: "creature_profiles",
                newName: "ix_npc_profiles_world_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_creature_profiles_creature_id",
                table: "creature_profiles",
                newName: "ix_npc_profiles_creature_id"
            );

            migrationBuilder.Sql(
                "ALTER TABLE creature_profiles RENAME CONSTRAINT pk_creature_profiles TO pk_npc_profiles;"
            );

            migrationBuilder.RenameTable(name: "creature_profiles", newName: "npc_profiles");

            migrationBuilder.CreateTable(
                name: "world_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    description = table.Column<string>(type: "text", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_events", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_world_events_location_id",
                table: "world_events",
                column: "location_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_world_events_world_id",
                table: "world_events",
                column: "world_id"
            );
        }
    }
}
