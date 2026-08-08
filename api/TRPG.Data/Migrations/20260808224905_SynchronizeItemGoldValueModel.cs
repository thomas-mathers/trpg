using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class SynchronizeItemGoldValueModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE items SET gold_value = 0 WHERE gold_value IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "gold_value",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "gold_value",
                table: "items",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer"
            );
        }
    }
}
