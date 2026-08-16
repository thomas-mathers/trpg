using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameJobToCreatureJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "jobs", newName: "creature_jobs");

            migrationBuilder.Sql(
                "ALTER TABLE creature_jobs RENAME CONSTRAINT pk_jobs TO pk_creature_jobs;"
            );

            migrationBuilder.RenameIndex(
                name: "ix_jobs_creature_id",
                table: "creature_jobs",
                newName: "ix_creature_jobs_creature_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_jobs_room_id",
                table: "creature_jobs",
                newName: "ix_creature_jobs_room_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_jobs_state_id_room_id",
                table: "creature_jobs",
                newName: "ix_creature_jobs_state_id_room_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_jobs_world_id",
                table: "creature_jobs",
                newName: "ix_creature_jobs_world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ix_creature_jobs_creature_id",
                table: "creature_jobs",
                newName: "ix_jobs_creature_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_creature_jobs_room_id",
                table: "creature_jobs",
                newName: "ix_jobs_room_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_creature_jobs_state_id_room_id",
                table: "creature_jobs",
                newName: "ix_jobs_state_id_room_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_creature_jobs_world_id",
                table: "creature_jobs",
                newName: "ix_jobs_world_id"
            );

            migrationBuilder.Sql(
                "ALTER TABLE creature_jobs RENAME CONSTRAINT pk_creature_jobs TO pk_jobs;"
            );

            migrationBuilder.RenameTable(name: "creature_jobs", newName: "jobs");
        }
    }
}
