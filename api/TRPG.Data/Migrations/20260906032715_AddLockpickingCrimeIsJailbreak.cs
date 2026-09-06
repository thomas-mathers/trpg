using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLockpickingCrimeIsJailbreak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TPH forces the column nullable, but the property is not, so existing rows need a value.
            migrationBuilder.AddColumn<bool>(
                name: "is_jailbreak",
                table: "crimes",
                type: "boolean",
                nullable: true,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "is_jailbreak", table: "crimes");
        }
    }
}
