using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeatherState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "weather_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condition = table.Column<string>(type: "text", nullable: false),
                    next_change_playtime = table.Column<TimeSpan>(
                        type: "interval",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weather_states", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_weather_states_state_id",
                table: "weather_states",
                column: "state_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_weather_states_world_id",
                table: "weather_states",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "weather_states");
        }
    }
}
