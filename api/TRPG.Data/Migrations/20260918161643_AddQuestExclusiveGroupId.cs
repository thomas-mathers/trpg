using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestExclusiveGroupId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "exclusive_group_id",
                table: "quests",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_quests_exclusive_group_id",
                table: "quests",
                column: "exclusive_group_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_quests_exclusive_group_id", table: "quests");

            migrationBuilder.DropColumn(name: "exclusive_group_id", table: "quests");
        }
    }
}
