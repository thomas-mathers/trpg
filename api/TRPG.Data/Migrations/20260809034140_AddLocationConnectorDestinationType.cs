using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationConnectorDestinationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "destination_type",
                table: "props",
                type: "text",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE props AS connector
                SET destination_type = CASE
                    WHEN destination.room_id IS NOT NULL THEN 'Room'
                    WHEN destination.district_id IS NOT NULL THEN 'District'
                    ELSE 'Wilderness'
                END
                FROM locations AS destination
                WHERE connector.behavior_type = 'LocationConnector'
                    AND connector.destination_location_id = destination.id;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "destination_type", table: "props");
        }
    }
}
