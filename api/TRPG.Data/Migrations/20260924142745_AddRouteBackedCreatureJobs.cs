using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRouteBackedCreatureJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "route_id",
                table: "creature_jobs",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_jobs_route_id",
                table: "creature_jobs",
                column: "route_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_creature_jobs_route_id", table: "creature_jobs");

            migrationBuilder.DropColumn(name: "route_id", table: "creature_jobs");
        }
    }
}
