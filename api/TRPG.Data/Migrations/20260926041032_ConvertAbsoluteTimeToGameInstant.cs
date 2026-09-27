using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConvertAbsoluteTimeToGameInstant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "playtime", table: "worlds");

            migrationBuilder.DropColumn(name: "next_change_playtime", table: "weather_states");

            migrationBuilder.DropColumn(name: "started_at_playtime", table: "route_travelers");

            migrationBuilder.DropColumn(name: "due_at_playtime", table: "room_bookings");

            migrationBuilder.DropColumn(name: "last_sync_playtime", table: "restock_policies");

            migrationBuilder.DropColumn(name: "last_sync_playtime", table: "quest_seed_schedules");

            migrationBuilder.DropColumn(name: "playtime", table: "game_sessions");

            migrationBuilder.DropColumn(name: "unlocks_at_playtime", table: "door_connectors");

            migrationBuilder.DropColumn(name: "last_regen_playtime", table: "creatures");

            migrationBuilder.DropColumn(name: "rested_until_playtime", table: "creatures");

            migrationBuilder.DropColumn(name: "last_sync_playtime", table: "creature_spawners");

            migrationBuilder.DropColumn(name: "purchased_at_playtime", table: "caravan_tickets");

            migrationBuilder.AddColumn<DateTime>(
                name: "game_time",
                table: "worlds",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "next_change_game_time",
                table: "weather_states",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "started_at_game_time",
                table: "route_travelers",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "due_at_game_time",
                table: "room_bookings",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "last_sync_game_time",
                table: "restock_policies",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "last_sync_game_time",
                table: "quest_seed_schedules",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "game_time",
                table: "game_sessions",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "unlocks_at_game_time",
                table: "door_connectors",
                type: "timestamp without time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "last_regen_game_time",
                table: "creatures",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "rested_until_game_time",
                table: "creatures",
                type: "timestamp without time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "last_sync_game_time",
                table: "creature_spawners",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "purchased_at_game_time",
                table: "caravan_tickets",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "game_time", table: "worlds");

            migrationBuilder.DropColumn(name: "next_change_game_time", table: "weather_states");

            migrationBuilder.DropColumn(name: "started_at_game_time", table: "route_travelers");

            migrationBuilder.DropColumn(name: "due_at_game_time", table: "room_bookings");

            migrationBuilder.DropColumn(name: "last_sync_game_time", table: "restock_policies");

            migrationBuilder.DropColumn(name: "last_sync_game_time", table: "quest_seed_schedules");

            migrationBuilder.DropColumn(name: "game_time", table: "game_sessions");

            migrationBuilder.DropColumn(name: "unlocks_at_game_time", table: "door_connectors");

            migrationBuilder.DropColumn(name: "last_regen_game_time", table: "creatures");

            migrationBuilder.DropColumn(name: "rested_until_game_time", table: "creatures");

            migrationBuilder.DropColumn(name: "last_sync_game_time", table: "creature_spawners");

            migrationBuilder.DropColumn(name: "purchased_at_game_time", table: "caravan_tickets");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "playtime",
                table: "worlds",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0)
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "next_change_playtime",
                table: "weather_states",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0)
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "started_at_playtime",
                table: "route_travelers",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0)
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "due_at_playtime",
                table: "room_bookings",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0)
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "last_sync_playtime",
                table: "restock_policies",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0)
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "last_sync_playtime",
                table: "quest_seed_schedules",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0)
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "playtime",
                table: "game_sessions",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0)
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "unlocks_at_playtime",
                table: "door_connectors",
                type: "interval",
                nullable: true
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "last_regen_playtime",
                table: "creatures",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0)
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "rested_until_playtime",
                table: "creatures",
                type: "interval",
                nullable: true
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "last_sync_playtime",
                table: "creature_spawners",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0)
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "purchased_at_playtime",
                table: "caravan_tickets",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0)
            );
        }
    }
}
