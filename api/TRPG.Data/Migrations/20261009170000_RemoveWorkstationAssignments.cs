using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    [DbContext(typeof(TrpgDbContext))]
    [Migration("20261009170000_RemoveWorkstationAssignments")]
    public partial class RemoveWorkstationAssignments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "workstation_assigned_creature_id", table: "props");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "workstation_assigned_creature_id",
                table: "props",
                type: "uuid",
                nullable: true
            );
        }
    }
}
