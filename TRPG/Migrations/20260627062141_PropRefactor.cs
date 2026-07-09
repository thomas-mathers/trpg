using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class PropRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "prop_items");

            migrationBuilder.DropColumn(name: "prop_type", table: "props");

            migrationBuilder.DropColumn(name: "seat_prop_occupant_id", table: "props");

            migrationBuilder.DropColumn(name: "sleeping_prop_assigned_person_id", table: "props");

            migrationBuilder.RenameColumn(
                name: "workstation_prop_occupant_id",
                table: "props",
                newName: "workstation_occupant_id"
            );

            migrationBuilder.RenameColumn(
                name: "workstation_prop_assigned_person_id",
                table: "props",
                newName: "workstation_assigned_person_id"
            );

            migrationBuilder.RenameColumn(
                name: "storage_prop_key_item_id",
                table: "props",
                newName: "room_connector_key_item_id"
            );

            migrationBuilder.RenameColumn(
                name: "sleeping_prop_occupant_id",
                table: "props",
                newName: "seat_occupant_id"
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

            migrationBuilder.AddColumn<string>(
                name: "workstation_type",
                table: "props",
                type: "text",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "container_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    index = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    container_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_container_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_container_items_props_container_id",
                        column: x => x.container_id,
                        principalTable: "props",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_container_items_container_id_index",
                table: "container_items",
                columns: new[] { "container_id", "index" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "container_items");

            migrationBuilder.DropColumn(name: "workstation_type", table: "props");

            migrationBuilder.RenameColumn(
                name: "workstation_occupant_id",
                table: "props",
                newName: "workstation_prop_occupant_id"
            );

            migrationBuilder.RenameColumn(
                name: "workstation_assigned_person_id",
                table: "props",
                newName: "workstation_prop_assigned_person_id"
            );

            migrationBuilder.RenameColumn(
                name: "seat_occupant_id",
                table: "props",
                newName: "sleeping_prop_occupant_id"
            );

            migrationBuilder.RenameColumn(
                name: "room_connector_key_item_id",
                table: "props",
                newName: "storage_prop_key_item_id"
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

            migrationBuilder.AddColumn<string>(
                name: "prop_type",
                table: "props",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

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

            migrationBuilder.CreateTable(
                name: "prop_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    index = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prop_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_prop_items_props_prop_id",
                        column: x => x.prop_id,
                        principalTable: "props",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_prop_items_prop_id_index",
                table: "prop_items",
                columns: new[] { "prop_id", "index" },
                unique: true
            );
        }
    }
}
