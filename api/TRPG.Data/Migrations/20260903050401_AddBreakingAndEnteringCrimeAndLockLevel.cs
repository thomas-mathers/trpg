using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBreakingAndEnteringCrimeAndLockLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "lock_level",
                table: "door_connectors",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AlterColumn<string>(
                name: "crime_type",
                table: "crimes",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5
            );

            migrationBuilder.AddColumn<Guid>(
                name: "break_enter_owner_faction_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "building_id",
                table: "crimes",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "building_name",
                table: "crimes",
                type: "text",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "lock_level", table: "door_connectors");

            migrationBuilder.DropColumn(name: "break_enter_owner_faction_id", table: "crimes");

            migrationBuilder.DropColumn(name: "building_id", table: "crimes");

            migrationBuilder.DropColumn(name: "building_name", table: "crimes");

            migrationBuilder.AlterColumn<string>(
                name: "crime_type",
                table: "crimes",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(13)",
                oldMaxLength: 13
            );
        }
    }
}
