using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeRouteTravelers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_guard_patrol_members",
                table: "guard_patrol_members"
            );

            migrationBuilder.DropIndex(
                name: "ix_guard_patrol_members_creature_id",
                table: "guard_patrol_members"
            );

            migrationBuilder.RenameTable(
                name: "guard_patrol_members",
                newName: "route_traveler_members"
            );

            migrationBuilder.RenameIndex(
                name: "ix_guard_patrol_members_route_traveler_id",
                table: "route_traveler_members",
                newName: "ix_route_traveler_members_route_traveler_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_guard_patrol_members_world_id",
                table: "route_traveler_members",
                newName: "ix_route_traveler_members_world_id"
            );

            migrationBuilder.AddPrimaryKey(
                name: "pk_route_traveler_members",
                table: "route_traveler_members",
                column: "id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_traveler_members_creature_id",
                table: "route_traveler_members",
                column: "creature_id",
                unique: true
            );

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "route_travelers",
                type: "text",
                nullable: false,
                defaultValue: "Caravan"
            );

            migrationBuilder.AddColumn<string>(
                name: "purpose",
                table: "route_travelers",
                type: "text",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE route_travelers
                SET kind = 'GuardPatrol'
                WHERE id IN (SELECT route_traveler_id FROM route_traveler_members)
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM route_traveler_members
                WHERE route_traveler_id IN (
                    SELECT id FROM route_travelers WHERE kind <> 'GuardPatrol'
                )
                """
            );

            migrationBuilder.DropPrimaryKey(
                name: "pk_route_traveler_members",
                table: "route_traveler_members"
            );

            migrationBuilder.DropIndex(
                name: "ix_route_traveler_members_creature_id",
                table: "route_traveler_members"
            );

            migrationBuilder.DropColumn(name: "kind", table: "route_travelers");

            migrationBuilder.DropColumn(name: "purpose", table: "route_travelers");

            migrationBuilder.RenameTable(
                name: "route_traveler_members",
                newName: "guard_patrol_members"
            );

            migrationBuilder.RenameIndex(
                name: "ix_route_traveler_members_route_traveler_id",
                table: "guard_patrol_members",
                newName: "ix_guard_patrol_members_route_traveler_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_route_traveler_members_world_id",
                table: "guard_patrol_members",
                newName: "ix_guard_patrol_members_world_id"
            );

            migrationBuilder.AddPrimaryKey(
                name: "pk_guard_patrol_members",
                table: "guard_patrol_members",
                column: "id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_guard_patrol_members_creature_id",
                table: "guard_patrol_members",
                column: "creature_id"
            );
        }
    }
}
