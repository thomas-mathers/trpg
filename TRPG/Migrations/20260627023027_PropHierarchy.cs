using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class PropHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "building_props");

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
                }
            );

            migrationBuilder.CreateTable(
                name: "props",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    storage_item_categories = table.Column<string>(type: "jsonb", nullable: true),
                    storage_size = table.Column<int>(type: "integer", nullable: true),
                    prop_type = table.Column<string>(
                        type: "character varying(13)",
                        maxLength: 13,
                        nullable: false
                    ),
                    bed_assigned_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    chair_occupant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    table_id = table.Column<Guid>(type: "uuid", nullable: true),
                    key_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occupant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    boundary = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_props", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_prop_items_prop_id_index",
                table: "prop_items",
                columns: new[] { "prop_id", "index" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_props_building_id",
                table: "props",
                column: "building_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "prop_items");

            migrationBuilder.DropTable(name: "props");

            migrationBuilder.CreateTable(
                name: "building_props",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    boundary = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_building_props", x => x.id);
                }
            );
        }
    }
}
