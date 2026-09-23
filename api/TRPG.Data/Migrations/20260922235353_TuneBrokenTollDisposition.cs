using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class TuneBrokenTollDisposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE factions
                SET aggression = 70,
                    reputation_sensitivity = 50,
                    risk_aversion = 35,
                    temperament = 'Predatory'
                WHERE name = 'The Broken Toll'
                  AND kind = 'Wilderness';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE factions
                SET aggression = 0,
                    reputation_sensitivity = 0,
                    risk_aversion = 0,
                    temperament = 'Authoritative'
                WHERE name = 'The Broken Toll'
                  AND kind = 'Wilderness';
                """
            );
        }
    }
}
