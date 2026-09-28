using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitCreatureStateIntoAxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE creatures ADD COLUMN condition text NOT NULL DEFAULT 'Awake';
                ALTER TABLE creatures ADD COLUMN activity text NULL;
                ALTER TABLE creatures ADD COLUMN movement text NOT NULL DEFAULT 'Stationary';

                UPDATE creatures SET condition = 'Sleeping' WHERE state = 'Sleeping';
                UPDATE creatures SET condition = 'Dead' WHERE state = 'Dead';
                UPDATE creatures SET movement = 'Walking' WHERE state = 'Walking';
                UPDATE creatures SET activity = state WHERE state IN ('Working', 'Studying', 'Praying', 'Eating');

                UPDATE creatures SET posture = 'Lying' WHERE posture = 'Laying';
                UPDATE creatures SET posture = 'Lying' WHERE condition = 'Sleeping';
                UPDATE creatures SET posture = 'Standing' WHERE condition = 'Awake' AND posture = 'Lying';
                UPDATE creatures SET posture = 'Standing' WHERE movement = 'Walking';
                UPDATE creatures SET activity = NULL, is_alerted = false, is_sneaking = false WHERE condition <> 'Awake';

                ALTER TABLE creatures ALTER COLUMN condition DROP DEFAULT;
                ALTER TABLE creatures ALTER COLUMN movement DROP DEFAULT;
                ALTER TABLE creatures DROP COLUMN state;

                ALTER TABLE route_travelers ADD COLUMN arrival_activity text NULL;
                UPDATE route_travelers SET arrival_activity = arrival_state
                    WHERE arrival_state IN ('Working', 'Studying', 'Praying', 'Eating');
                ALTER TABLE route_travelers DROP COLUMN arrival_state;
                """
            );

            migrationBuilder.AddCheckConstraint(
                name: "ck_creatures_activity_requires_awake",
                table: "creatures",
                sql: "activity IS NULL OR condition = 'Awake'"
            );

            migrationBuilder.AddCheckConstraint(
                name: "ck_creatures_alerted_requires_awake",
                table: "creatures",
                sql: "NOT is_alerted OR condition = 'Awake'"
            );

            migrationBuilder.AddCheckConstraint(
                name: "ck_creatures_lying_requires_not_awake",
                table: "creatures",
                sql: "posture <> 'Lying' OR condition <> 'Awake'"
            );

            migrationBuilder.AddCheckConstraint(
                name: "ck_creatures_sleeping_requires_lying",
                table: "creatures",
                sql: "condition <> 'Sleeping' OR posture = 'Lying'"
            );

            migrationBuilder.AddCheckConstraint(
                name: "ck_creatures_sneaking_requires_awake",
                table: "creatures",
                sql: "NOT is_sneaking OR condition = 'Awake'"
            );

            migrationBuilder.AddCheckConstraint(
                name: "ck_creatures_walking_requires_awake",
                table: "creatures",
                sql: "movement = 'Stationary' OR condition = 'Awake'"
            );

            migrationBuilder.AddCheckConstraint(
                name: "ck_creatures_walking_requires_standing",
                table: "creatures",
                sql: "movement = 'Stationary' OR posture = 'Standing'"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_creatures_activity_requires_awake",
                table: "creatures"
            );

            migrationBuilder.DropCheckConstraint(
                name: "ck_creatures_alerted_requires_awake",
                table: "creatures"
            );

            migrationBuilder.DropCheckConstraint(
                name: "ck_creatures_lying_requires_not_awake",
                table: "creatures"
            );

            migrationBuilder.DropCheckConstraint(
                name: "ck_creatures_sleeping_requires_lying",
                table: "creatures"
            );

            migrationBuilder.DropCheckConstraint(
                name: "ck_creatures_sneaking_requires_awake",
                table: "creatures"
            );

            migrationBuilder.DropCheckConstraint(
                name: "ck_creatures_walking_requires_awake",
                table: "creatures"
            );

            migrationBuilder.DropCheckConstraint(
                name: "ck_creatures_walking_requires_standing",
                table: "creatures"
            );

            migrationBuilder.Sql(
                """
                ALTER TABLE creatures ADD COLUMN state text NOT NULL DEFAULT 'Idle';
                UPDATE creatures SET state = activity WHERE activity IS NOT NULL;
                UPDATE creatures SET state = 'Walking' WHERE movement = 'Walking';
                UPDATE creatures SET state = condition WHERE condition <> 'Awake';
                ALTER TABLE creatures ALTER COLUMN state DROP DEFAULT;
                UPDATE creatures SET posture = 'Laying' WHERE posture = 'Lying';
                ALTER TABLE creatures DROP COLUMN condition;
                ALTER TABLE creatures DROP COLUMN activity;
                ALTER TABLE creatures DROP COLUMN movement;

                ALTER TABLE route_travelers ADD COLUMN arrival_state text NOT NULL DEFAULT 'Idle';
                UPDATE route_travelers SET arrival_state = arrival_activity WHERE arrival_activity IS NOT NULL;
                ALTER TABLE route_travelers ALTER COLUMN arrival_state DROP DEFAULT;
                ALTER TABLE route_travelers DROP COLUMN arrival_activity;
                """
            );
        }
    }
}
