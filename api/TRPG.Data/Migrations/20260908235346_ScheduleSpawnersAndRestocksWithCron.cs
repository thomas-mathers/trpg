using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleSpawnersAndRestocksWithCron : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "creature_spawners", "restock_policies" })
            {
                migrationBuilder.AddColumn<string>(
                    name: "schedule",
                    table: table,
                    type: "text",
                    nullable: false,
                    defaultValue: "0 0 * * *"
                );

                // Carry the existing schedules across rather than leaving every shop and spawner
                // holding an unparseable one, which reads as "never fires".
                migrationBuilder.Sql(
                    $"""
                    UPDATE {table}
                    SET schedule = '0 ' || trigger_hour || ' * * ' || COALESCE(
                        CASE specific_day
                            WHEN 'Sunday' THEN '0'
                            WHEN 'Monday' THEN '1'
                            WHEN 'Tuesday' THEN '2'
                            WHEN 'Wednesday' THEN '3'
                            WHEN 'Thursday' THEN '4'
                            WHEN 'Friday' THEN '5'
                            WHEN 'Saturday' THEN '6'
                        END, '*')
                    """
                );

                migrationBuilder.DropColumn(name: "specific_day", table: table);
                migrationBuilder.DropColumn(name: "trigger_hour", table: table);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "creature_spawners", "restock_policies" })
            {
                migrationBuilder.AddColumn<int>(
                    name: "trigger_hour",
                    table: table,
                    type: "integer",
                    nullable: false,
                    defaultValue: 0
                );

                migrationBuilder.AddColumn<string>(
                    name: "specific_day",
                    table: table,
                    type: "text",
                    nullable: true
                );

                migrationBuilder.Sql(
                    $"""
                    UPDATE {table}
                    SET trigger_hour = COALESCE(NULLIF(split_part(schedule, ' ', 2), '*'), '0')::int
                    """
                );

                migrationBuilder.DropColumn(name: "schedule", table: table);
            }
        }
    }
}
