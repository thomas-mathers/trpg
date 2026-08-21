using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContainerOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_creature_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE props AS prop
                SET owner_creature_id = (
                    SELECT building_owner.owner_id
                    FROM rooms AS room
                    JOIN building_owners AS building_owner ON building_owner.building_id = room.building_id
                    WHERE room.location_id = prop.location_id
                    ORDER BY building_owner.owner_id
                    LIMIT 1
                )
                WHERE prop.behavior_type = 'Container' AND prop.owner_creature_id IS NULL;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "owner_creature_id", table: "props");
        }
    }
}
