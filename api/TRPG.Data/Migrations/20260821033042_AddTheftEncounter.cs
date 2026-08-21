using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTheftEncounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "item_ids",
                table: "encounters",
                type: "uuid[]",
                nullable: true
            );

            migrationBuilder.AddColumn<List<string>>(
                name: "item_names",
                table: "encounters",
                type: "text[]",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "item_selections",
                table: "encounters",
                type: "jsonb",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "owner_creature_id",
                table: "encounters",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "owner_name",
                table: "encounters",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "source_owner_id",
                table: "encounters",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "source_owner_type",
                table: "encounters",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "theft_crime_id",
                table: "encounters",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<List<Guid>>(
                name: "witness_creature_ids",
                table: "encounters",
                type: "uuid[]",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "item_ids", table: "encounters");

            migrationBuilder.DropColumn(name: "item_names", table: "encounters");

            migrationBuilder.DropColumn(name: "item_selections", table: "encounters");

            migrationBuilder.DropColumn(name: "owner_creature_id", table: "encounters");

            migrationBuilder.DropColumn(name: "owner_name", table: "encounters");

            migrationBuilder.DropColumn(name: "source_owner_id", table: "encounters");

            migrationBuilder.DropColumn(name: "source_owner_type", table: "encounters");

            migrationBuilder.DropColumn(name: "theft_crime_id", table: "encounters");

            migrationBuilder.DropColumn(name: "witness_creature_ids", table: "encounters");
        }
    }
}
