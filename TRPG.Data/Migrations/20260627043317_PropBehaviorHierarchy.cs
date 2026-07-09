using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class PropBehaviorHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "bed_assigned_person_id", table: "props");

            migrationBuilder.DropColumn(name: "chair_occupant_id", table: "props");

            migrationBuilder.DropColumn(name: "staircase_destination_room_id", table: "props");

            migrationBuilder.DropColumn(name: "table_id", table: "props");

            migrationBuilder.RenameColumn(
                name: "chest_key_item_id",
                table: "props",
                newName: "storage_prop_key_item_id"
            );

            migrationBuilder.AlterColumn<string>(
                name: "prop_type",
                table: "props",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(13)",
                oldMaxLength: 13
            );

            migrationBuilder.AddColumn<string>(
                name: "behavior_type",
                table: "props",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddForeignKey(
                name: "fk_prop_items_props_prop_id",
                table: "prop_items",
                column: "prop_id",
                principalTable: "props",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_prop_items_props_prop_id",
                table: "prop_items"
            );

            migrationBuilder.DropColumn(name: "behavior_type", table: "props");

            migrationBuilder.RenameColumn(
                name: "storage_prop_key_item_id",
                table: "props",
                newName: "chest_key_item_id"
            );

            migrationBuilder.AlterColumn<string>(
                name: "prop_type",
                table: "props",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "bed_assigned_person_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "chair_occupant_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "staircase_destination_room_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "table_id",
                table: "props",
                type: "uuid",
                nullable: true
            );
        }
    }
}
