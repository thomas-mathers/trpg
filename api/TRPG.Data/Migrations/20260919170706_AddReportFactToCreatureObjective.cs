using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportFactToCreatureObjective : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "report_fact_to_creature_objective_creature_id",
                table: "quest_objectives",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "report_fact_to_creature_objective_fact_id",
                table: "quest_objectives",
                type: "uuid",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "report_fact_to_creature_objective_creature_id",
                table: "quest_objectives"
            );

            migrationBuilder.DropColumn(
                name: "report_fact_to_creature_objective_fact_id",
                table: "quest_objectives"
            );
        }
    }
}
