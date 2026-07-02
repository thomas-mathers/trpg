using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyLocationAndCircle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_persons_location_region_id_location_building_id",
                table: "persons");

            migrationBuilder.DropIndex(
                name: "ix_jobs_location_region_id_location_building_id",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "region",
                table: "world_events");

            migrationBuilder.DropColumn(
                name: "region",
                table: "quest_objectives");

            migrationBuilder.DropColumn(
                name: "location_building_id",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "location_coordinates_x",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "location_coordinates_y",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "location_building_id",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "location_coordinates_x",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "location_coordinates_y",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "boundary",
                table: "buildings");

            migrationBuilder.RenameColumn(
                name: "location_room_id",
                table: "persons",
                newName: "room_id");

            migrationBuilder.RenameColumn(
                name: "location_region_id",
                table: "persons",
                newName: "region_id");

            migrationBuilder.RenameColumn(
                name: "location_room_id",
                table: "jobs",
                newName: "room_id");

            migrationBuilder.RenameColumn(
                name: "location_region_id",
                table: "jobs",
                newName: "region_id");

            migrationBuilder.AddColumn<Guid>(
                name: "region_id",
                table: "world_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "region_id",
                table: "quest_objectives",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "region_id",
                table: "persons",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "region_id",
                table: "jobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_persons_region_id_room_id",
                table: "persons",
                columns: new[] { "region_id", "room_id" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_region_id_room_id",
                table: "jobs",
                columns: new[] { "region_id", "room_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_persons_region_id_room_id",
                table: "persons");

            migrationBuilder.DropIndex(
                name: "ix_jobs_region_id_room_id",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "region_id",
                table: "world_events");

            migrationBuilder.DropColumn(
                name: "region_id",
                table: "quest_objectives");

            migrationBuilder.RenameColumn(
                name: "room_id",
                table: "persons",
                newName: "location_room_id");

            migrationBuilder.RenameColumn(
                name: "region_id",
                table: "persons",
                newName: "location_region_id");

            migrationBuilder.RenameColumn(
                name: "room_id",
                table: "jobs",
                newName: "location_room_id");

            migrationBuilder.RenameColumn(
                name: "region_id",
                table: "jobs",
                newName: "location_region_id");

            migrationBuilder.AddColumn<string>(
                name: "region",
                table: "world_events",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "region",
                table: "quest_objectives",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AlterColumn<Guid>(
                name: "location_region_id",
                table: "persons",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "location_building_id",
                table: "persons",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "location_coordinates_x",
                table: "persons",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "location_coordinates_y",
                table: "persons",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AlterColumn<Guid>(
                name: "location_region_id",
                table: "jobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "location_building_id",
                table: "jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "location_coordinates_x",
                table: "jobs",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "location_coordinates_y",
                table: "jobs",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "boundary",
                table: "buildings",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateIndex(
                name: "ix_persons_location_region_id_location_building_id",
                table: "persons",
                columns: new[] { "location_region_id", "location_building_id" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_location_region_id_location_building_id",
                table: "jobs",
                columns: new[] { "location_region_id", "location_building_id" });
        }
    }
}
