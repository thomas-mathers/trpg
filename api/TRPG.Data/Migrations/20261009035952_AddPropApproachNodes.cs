using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPropApproachNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "approach_node_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_props_approach_node_id",
                table: "props",
                column: "approach_node_id"
            );

            migrationBuilder.AddForeignKey(
                name: "fk_props_travel_nodes_approach_node_id",
                table: "props",
                column: "approach_node_id",
                principalTable: "travel_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_props_travel_nodes_approach_node_id",
                table: "props"
            );

            migrationBuilder.DropIndex(name: "ix_props_approach_node_id", table: "props");

            migrationBuilder.DropColumn(name: "approach_node_id", table: "props");
        }
    }
}
