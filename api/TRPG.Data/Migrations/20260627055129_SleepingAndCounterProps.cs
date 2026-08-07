using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class SleepingAndCounterProps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "seat_prop_occupant_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "sleeping_prop_assigned_person_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "sleeping_prop_occupant_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "workstation_prop_assigned_person_id",
                table: "props",
                type: "uuid",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "seat_prop_occupant_id", table: "props");

            migrationBuilder.DropColumn(name: "sleeping_prop_assigned_person_id", table: "props");

            migrationBuilder.DropColumn(name: "sleeping_prop_occupant_id", table: "props");

            migrationBuilder.DropColumn(
                name: "workstation_prop_assigned_person_id",
                table: "props"
            );
        }
    }
}
