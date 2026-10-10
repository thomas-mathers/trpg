using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePropTravelNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_journeys_props_destination_prop_id",
                table: "journeys"
            );

            migrationBuilder.DropForeignKey(
                name: "fk_props_travel_nodes_approach_node_id",
                table: "props"
            );

            migrationBuilder.DropIndex(name: "ix_props_approach_node_id", table: "props");

            migrationBuilder.DropIndex(name: "ix_journeys_destination_prop_id", table: "journeys");

            migrationBuilder.DropColumn(name: "approach_node_id", table: "props");

            migrationBuilder.DropColumn(name: "destination_prop_id", table: "journeys");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "approach_node_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "destination_prop_id",
                table: "journeys",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_props_approach_node_id",
                table: "props",
                column: "approach_node_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journeys_destination_prop_id",
                table: "journeys",
                column: "destination_prop_id"
            );

            migrationBuilder.AddForeignKey(
                name: "fk_journeys_props_destination_prop_id",
                table: "journeys",
                column: "destination_prop_id",
                principalTable: "props",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict
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
    }
}
