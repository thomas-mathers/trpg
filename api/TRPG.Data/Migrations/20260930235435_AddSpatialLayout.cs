using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSpatialLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "angle",
                table: "props",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "depth",
                table: "props",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "width",
                table: "props",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "x",
                table: "props",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "y",
                table: "props",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "depth",
                table: "locations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "width",
                table: "locations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "arrival_angle",
                table: "location_connectors",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "arrival_x",
                table: "location_connectors",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "arrival_y",
                table: "location_connectors",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "exit_x",
                table: "location_connectors",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "exit_y",
                table: "location_connectors",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "angle",
                table: "creatures",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "x",
                table: "creatures",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "y",
                table: "creatures",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "angle",
                table: "buildings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "depth",
                table: "buildings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "width",
                table: "buildings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "x",
                table: "buildings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "y",
                table: "buildings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "angle", table: "props");

            migrationBuilder.DropColumn(name: "depth", table: "props");

            migrationBuilder.DropColumn(name: "width", table: "props");

            migrationBuilder.DropColumn(name: "x", table: "props");

            migrationBuilder.DropColumn(name: "y", table: "props");

            migrationBuilder.DropColumn(name: "depth", table: "locations");

            migrationBuilder.DropColumn(name: "width", table: "locations");

            migrationBuilder.DropColumn(name: "arrival_angle", table: "location_connectors");

            migrationBuilder.DropColumn(name: "arrival_x", table: "location_connectors");

            migrationBuilder.DropColumn(name: "arrival_y", table: "location_connectors");

            migrationBuilder.DropColumn(name: "exit_x", table: "location_connectors");

            migrationBuilder.DropColumn(name: "exit_y", table: "location_connectors");

            migrationBuilder.DropColumn(name: "angle", table: "creatures");

            migrationBuilder.DropColumn(name: "x", table: "creatures");

            migrationBuilder.DropColumn(name: "y", table: "creatures");

            migrationBuilder.DropColumn(name: "angle", table: "buildings");

            migrationBuilder.DropColumn(name: "depth", table: "buildings");

            migrationBuilder.DropColumn(name: "width", table: "buildings");

            migrationBuilder.DropColumn(name: "x", table: "buildings");

            migrationBuilder.DropColumn(name: "y", table: "buildings");
        }
    }
}
