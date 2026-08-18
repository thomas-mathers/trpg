using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class NpcProfilesAndConversationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_npc_conversations_npc_id_creature_id",
                table: "npc_conversations"
            );

            migrationBuilder.DropColumn(name: "creature_id", table: "npc_conversations");

            migrationBuilder.RenameColumn(
                name: "npc_id",
                table: "npc_conversations",
                newName: "npc_conversation_history_id"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "npc_conversations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.CreateTable(
                name: "npc_conversation_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    npc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_npc_conversation_histories", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "npc_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appearance = table.Column<string>(type: "jsonb", nullable: false),
                    behavior = table.Column<string>(type: "jsonb", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    private_background = table.Column<string>(type: "jsonb", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_npc_profiles", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "quest_reputation_rewards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quest_reputation_rewards", x => x.id);
                    table.ForeignKey(
                        name: "fk_quest_reputation_rewards_quests_quest_id",
                        column: x => x.quest_id,
                        principalTable: "quests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.AddCheckConstraint(
                name: "ck_reputations_score",
                table: "reputations",
                sql: "score BETWEEN -100 AND 100"
            );

            migrationBuilder.CreateIndex(
                name: "ix_npc_conversations_npc_conversation_history_id_created_at",
                table: "npc_conversations",
                columns: new[] { "npc_conversation_history_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_npc_conversation_histories_npc_id_creature_id",
                table: "npc_conversation_histories",
                columns: new[] { "npc_id", "creature_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_npc_conversation_histories_world_id",
                table: "npc_conversation_histories",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_npc_profiles_creature_id",
                table: "npc_profiles",
                column: "creature_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_npc_profiles_world_id",
                table: "npc_profiles",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_quest_reputation_rewards_quest_id",
                table: "quest_reputation_rewards",
                column: "quest_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_quest_reputation_rewards_world_id",
                table: "quest_reputation_rewards",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "npc_conversation_histories");

            migrationBuilder.DropTable(name: "npc_profiles");

            migrationBuilder.DropTable(name: "quest_reputation_rewards");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reputations_score",
                table: "reputations"
            );

            migrationBuilder.DropIndex(
                name: "ix_npc_conversations_npc_conversation_history_id_created_at",
                table: "npc_conversations"
            );

            migrationBuilder.DropColumn(name: "created_at", table: "npc_conversations");

            migrationBuilder.RenameColumn(
                name: "npc_conversation_history_id",
                table: "npc_conversations",
                newName: "npc_id"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "creature_id",
                table: "npc_conversations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.CreateIndex(
                name: "ix_npc_conversations_npc_id_creature_id",
                table: "npc_conversations",
                columns: new[] { "npc_id", "creature_id" },
                unique: true
            );
        }
    }
}
