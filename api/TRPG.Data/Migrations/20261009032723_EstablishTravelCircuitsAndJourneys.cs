using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class EstablishTravelCircuitsAndJourneys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "caravan_fares");

            migrationBuilder.DropTable(name: "caravan_tickets");

            migrationBuilder.DropTable(name: "route_steps");

            migrationBuilder.DropTable(name: "route_traveler_members");

            migrationBuilder.DropTable(name: "route_travelers");

            migrationBuilder.DropTable(name: "routes");

            migrationBuilder.DropIndex(name: "ix_creature_jobs_route_id", table: "creature_jobs");

            migrationBuilder.DropColumn(name: "route_id", table: "creature_jobs");

            migrationBuilder.AddColumn<Guid>(
                name: "current_travel_node_id",
                table: "creatures",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "travel_circuits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_travel_circuits", x => x.id);
                    table.ForeignKey(
                        name: "fk_travel_circuits_worlds_world_id",
                        column: x => x.world_id,
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "journeys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    travel_circuit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purpose = table.Column<string>(type: "text", nullable: true),
                    arrival_activity = table.Column<string>(type: "text", nullable: true),
                    destination_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    destination_prop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    planned_at = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: false
                    ),
                    departure_at = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: false
                    ),
                    checkpoint_leg_index = table.Column<int>(type: "integer", nullable: false),
                    checkpoint_leg_progress_meters = table.Column<double>(
                        type: "double precision",
                        nullable: false
                    ),
                    checkpointed_at = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: false
                    ),
                    paused_at = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journeys", x => x.id);
                    table.ForeignKey(
                        name: "fk_journeys_creature_jobs_destination_job_id",
                        column: x => x.destination_job_id,
                        principalTable: "creature_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_journeys_props_destination_prop_id",
                        column: x => x.destination_prop_id,
                        principalTable: "props",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_journeys_travel_circuits_travel_circuit_id",
                        column: x => x.travel_circuit_id,
                        principalTable: "travel_circuits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_journeys_worlds_world_id",
                        column: x => x.world_id,
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "travel_circuit_legs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    travel_circuit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    index = table.Column<int>(type: "integer", nullable: false),
                    from_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dwell_after = table.Column<TimeSpan>(type: "interval", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_travel_circuit_legs", x => x.id);
                    table.ForeignKey(
                        name: "fk_travel_circuit_legs_connectors_connector_id",
                        column: x => x.connector_id,
                        principalTable: "connectors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_travel_circuit_legs_travel_circuits_travel_circuit_id",
                        column: x => x.travel_circuit_id,
                        principalTable: "travel_circuits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_travel_circuit_legs_travel_nodes_from_node_id",
                        column: x => x.from_node_id,
                        principalTable: "travel_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_travel_circuit_legs_travel_nodes_to_node_id",
                        column: x => x.to_node_id,
                        principalTable: "travel_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "journey_legs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    journey_id = table.Column<Guid>(type: "uuid", nullable: false),
                    index = table.Column<int>(type: "integer", nullable: false),
                    from_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    distance = table.Column<double>(type: "double precision", nullable: false),
                    dwell_after = table.Column<TimeSpan>(type: "interval", nullable: false),
                    path = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journey_legs", x => x.id);
                    table.ForeignKey(
                        name: "fk_journey_legs_connectors_connector_id",
                        column: x => x.connector_id,
                        principalTable: "connectors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_journey_legs_journeys_journey_id",
                        column: x => x.journey_id,
                        principalTable: "journeys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_journey_legs_travel_nodes_from_node_id",
                        column: x => x.from_node_id,
                        principalTable: "travel_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_journey_legs_travel_nodes_to_node_id",
                        column: x => x.to_node_id,
                        principalTable: "travel_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "journey_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    journey_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journey_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_journey_members_creatures_creature_id",
                        column: x => x.creature_id,
                        principalTable: "creatures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_journey_members_journeys_journey_id",
                        column: x => x.journey_id,
                        principalTable: "journeys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_creatures_current_travel_node_id",
                table: "creatures",
                column: "current_travel_node_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journey_legs_connector_id",
                table: "journey_legs",
                column: "connector_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journey_legs_from_node_id",
                table: "journey_legs",
                column: "from_node_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journey_legs_journey_id",
                table: "journey_legs",
                column: "journey_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journey_legs_journey_id_index",
                table: "journey_legs",
                columns: new[] { "journey_id", "index" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_journey_legs_to_node_id",
                table: "journey_legs",
                column: "to_node_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journey_members_creature_id",
                table: "journey_members",
                column: "creature_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journey_members_journey_id",
                table: "journey_members",
                column: "journey_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journey_members_journey_id_creature_id",
                table: "journey_members",
                columns: new[] { "journey_id", "creature_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_journeys_destination_job_id",
                table: "journeys",
                column: "destination_job_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journeys_destination_prop_id",
                table: "journeys",
                column: "destination_prop_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journeys_travel_circuit_id",
                table: "journeys",
                column: "travel_circuit_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journeys_world_id",
                table: "journeys",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_travel_circuit_legs_connector_id",
                table: "travel_circuit_legs",
                column: "connector_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_travel_circuit_legs_from_node_id",
                table: "travel_circuit_legs",
                column: "from_node_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_travel_circuit_legs_to_node_id",
                table: "travel_circuit_legs",
                column: "to_node_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_travel_circuit_legs_travel_circuit_id",
                table: "travel_circuit_legs",
                column: "travel_circuit_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_travel_circuit_legs_travel_circuit_id_index",
                table: "travel_circuit_legs",
                columns: new[] { "travel_circuit_id", "index" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_travel_circuits_world_id",
                table: "travel_circuits",
                column: "world_id"
            );

            migrationBuilder.AddForeignKey(
                name: "fk_creatures_travel_nodes_current_travel_node_id",
                table: "creatures",
                column: "current_travel_node_id",
                principalTable: "travel_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.Sql(
                """
                CREATE FUNCTION ensure_travel_circuit_is_closed(circuit_id uuid) RETURNS void AS $$
                DECLARE
                    first_from_node_id uuid;
                    last_to_node_id uuid;
                BEGIN
                    SELECT from_node_id INTO first_from_node_id
                    FROM travel_circuit_legs
                    WHERE travel_circuit_id = circuit_id
                    ORDER BY index
                    LIMIT 1;

                    SELECT to_node_id INTO last_to_node_id
                    FROM travel_circuit_legs
                    WHERE travel_circuit_id = circuit_id
                    ORDER BY index DESC
                    LIMIT 1;

                    IF first_from_node_id IS NOT NULL AND first_from_node_id <> last_to_node_id THEN
                        RAISE EXCEPTION 'Travel circuit legs must form a closed sequence.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM (
                            SELECT
                                from_node_id,
                                LAG(to_node_id) OVER (ORDER BY index) AS previous_to_node_id
                            FROM travel_circuit_legs
                            WHERE travel_circuit_id = circuit_id
                        ) legs
                        WHERE previous_to_node_id IS NOT NULL AND previous_to_node_id <> from_node_id
                    ) THEN
                        RAISE EXCEPTION 'Travel circuit legs must connect in index order.';
                    END IF;
                END;
                $$ LANGUAGE plpgsql;
                CREATE FUNCTION enforce_travel_circuit_closure() RETURNS trigger AS $$
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        PERFORM ensure_travel_circuit_is_closed(OLD.travel_circuit_id);
                    END IF;

                    IF TG_OP <> 'DELETE' THEN
                        PERFORM ensure_travel_circuit_is_closed(NEW.travel_circuit_id);
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                CREATE CONSTRAINT TRIGGER enforce_travel_circuit_closure
                AFTER INSERT OR UPDATE OR DELETE ON travel_circuit_legs
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION enforce_travel_circuit_closure();
                CREATE FUNCTION enforce_active_journey_membership() RETURNS trigger AS $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM journey_members existing_member
                        JOIN journeys existing_journey ON existing_journey.id = existing_member.journey_id
                        JOIN journeys candidate_journey ON candidate_journey.id = NEW.journey_id
                        WHERE existing_member.creature_id = NEW.creature_id
                          AND existing_member.id <> NEW.id
                          AND existing_journey.status IN ('Planned', 'Traveling', 'Dwelling')
                          AND candidate_journey.status IN ('Planned', 'Traveling', 'Dwelling')) THEN
                        RAISE EXCEPTION 'Creature already belongs to an active journey.';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER enforce_active_journey_membership
                BEFORE INSERT OR UPDATE ON journey_members
                FOR EACH ROW EXECUTE FUNCTION enforce_active_journey_membership();
                CREATE FUNCTION enforce_active_journey_status() RETURNS trigger AS $$
                BEGIN
                    IF NEW.status IN ('Planned', 'Traveling', 'Dwelling') AND EXISTS (
                        SELECT 1
                        FROM journey_members candidate_member
                        JOIN journey_members existing_member ON existing_member.creature_id = candidate_member.creature_id
                        JOIN journeys existing_journey ON existing_journey.id = existing_member.journey_id
                        WHERE candidate_member.journey_id = NEW.id
                          AND existing_member.journey_id <> NEW.id
                          AND existing_journey.status IN ('Planned', 'Traveling', 'Dwelling')) THEN
                        RAISE EXCEPTION 'Creature already belongs to an active journey.';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER enforce_active_journey_status
                BEFORE UPDATE OF status ON journeys
                FOR EACH ROW EXECUTE FUNCTION enforce_active_journey_status();
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS enforce_travel_circuit_closure ON travel_circuit_legs; DROP TRIGGER IF EXISTS enforce_active_journey_membership ON journey_members; DROP TRIGGER IF EXISTS enforce_active_journey_status ON journeys; DROP FUNCTION IF EXISTS enforce_travel_circuit_closure(); DROP FUNCTION IF EXISTS ensure_travel_circuit_is_closed(uuid); DROP FUNCTION IF EXISTS enforce_active_journey_membership(); DROP FUNCTION IF EXISTS enforce_active_journey_status();"
            );

            migrationBuilder.DropForeignKey(
                name: "fk_creatures_travel_nodes_current_travel_node_id",
                table: "creatures"
            );

            migrationBuilder.DropTable(name: "journey_legs");

            migrationBuilder.DropTable(name: "journey_members");

            migrationBuilder.DropTable(name: "travel_circuit_legs");

            migrationBuilder.DropTable(name: "journeys");

            migrationBuilder.DropTable(name: "travel_circuits");

            migrationBuilder.DropIndex(
                name: "ix_creatures_current_travel_node_id",
                table: "creatures"
            );

            migrationBuilder.DropColumn(name: "current_travel_node_id", table: "creatures");

            migrationBuilder.AddColumn<Guid>(
                name: "route_id",
                table: "creature_jobs",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "caravan_fares",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_fee_gold = table.Column<int>(type: "integer", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caravan_fares", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "caravan_tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_stop_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchased_at_game_time = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: false
                    ),
                    route_traveler_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caravan_tickets", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "route_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_id = table.Column<Guid>(type: "uuid", nullable: true),
                    distance = table.Column<double>(type: "double precision", nullable: false),
                    dwell_hours = table.Column<double>(type: "double precision", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_index = table.Column<int>(type: "integer", nullable: false),
                    travel_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_steps", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "route_traveler_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_traveler_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_traveler_members", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "route_travelers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arrival_activity = table.Column<string>(type: "text", nullable: true),
                    paused_at_game_time = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: true
                    ),
                    purpose = table.Column<string>(type: "text", nullable: true),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    speed_units_per_hour = table.Column<double>(
                        type: "double precision",
                        nullable: false
                    ),
                    started_at_game_time = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: false
                    ),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_travelers", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_routes", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_jobs_route_id",
                table: "creature_jobs",
                column: "route_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravan_fares_route_id",
                table: "caravan_fares",
                column: "route_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravan_fares_world_id",
                table: "caravan_fares",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravan_tickets_creature_id",
                table: "caravan_tickets",
                column: "creature_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravan_tickets_route_traveler_id",
                table: "caravan_tickets",
                column: "route_traveler_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravan_tickets_world_id",
                table: "caravan_tickets",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_connector_id",
                table: "route_steps",
                column: "connector_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_route_id",
                table: "route_steps",
                column: "route_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_route_id_sequence_index",
                table: "route_steps",
                columns: new[] { "route_id", "sequence_index" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_world_id",
                table: "route_steps",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_world_id_location_id_route_id",
                table: "route_steps",
                columns: new[] { "world_id", "location_id", "route_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_traveler_members_creature_id",
                table: "route_traveler_members",
                column: "creature_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_traveler_members_route_traveler_id",
                table: "route_traveler_members",
                column: "route_traveler_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_traveler_members_world_id",
                table: "route_traveler_members",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_travelers_route_id",
                table: "route_travelers",
                column: "route_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_travelers_world_id",
                table: "route_travelers",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_routes_world_id",
                table: "routes",
                column: "world_id"
            );
        }
    }
}
