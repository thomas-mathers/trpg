using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameRoomConnectorToLocationConnectorAndAddDestinationLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "room_connector_keys",
                newName: "location_connector_keys"
            );

            migrationBuilder.RenameColumn(
                name: "room_connector_id",
                table: "location_connector_keys",
                newName: "location_connector_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_room_connector_keys_room_connector_id",
                table: "location_connector_keys",
                newName: "ix_location_connector_keys_location_connector_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_room_connector_keys_item_id",
                table: "location_connector_keys",
                newName: "ix_location_connector_keys_item_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_room_connector_keys_world_id",
                table: "location_connector_keys",
                newName: "ix_location_connector_keys_world_id"
            );

            migrationBuilder.Sql(
                "ALTER TABLE location_connector_keys RENAME CONSTRAINT pk_room_connector_keys TO pk_location_connector_keys;"
            );

            migrationBuilder.AlterColumn<string>(
                name: "behavior_type",
                table: "props",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(13)",
                oldMaxLength: 13
            );

            migrationBuilder.Sql(
                "UPDATE props SET behavior_type = 'LocationConnector' WHERE behavior_type = 'RoomConnector';"
            );

            migrationBuilder.AddColumn<string>(
                name: "destination_label",
                table: "props",
                type: "text",
                nullable: false,
                defaultValue: "Outside"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "destination_label", table: "props");

            migrationBuilder.Sql(
                "UPDATE props SET behavior_type = 'RoomConnector' WHERE behavior_type = 'LocationConnector';"
            );

            migrationBuilder.AlterColumn<string>(
                name: "behavior_type",
                table: "props",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(21)",
                oldMaxLength: 21
            );

            migrationBuilder.Sql(
                "ALTER TABLE location_connector_keys RENAME CONSTRAINT pk_location_connector_keys TO pk_room_connector_keys;"
            );

            migrationBuilder.RenameIndex(
                name: "ix_location_connector_keys_world_id",
                table: "location_connector_keys",
                newName: "ix_room_connector_keys_world_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_location_connector_keys_item_id",
                table: "location_connector_keys",
                newName: "ix_room_connector_keys_item_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_location_connector_keys_location_connector_id",
                table: "location_connector_keys",
                newName: "ix_room_connector_keys_room_connector_id"
            );

            migrationBuilder.RenameColumn(
                name: "location_connector_id",
                table: "location_connector_keys",
                newName: "room_connector_id"
            );

            migrationBuilder.RenameTable(
                name: "location_connector_keys",
                newName: "room_connector_keys"
            );
        }
    }
}
