using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations;

public partial class ConvertQuestObjectivesToTph : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM quest_objectives
                    WHERE (type, target_type) NOT IN (
                        ('Kill', 'Creature'),
                        ('Collect', 'Item'),
                        ('Explore', 'Building'),
                        ('Explore', 'City'),
                        ('Speak', 'Creature')
                    )
                ) THEN
                    RAISE EXCEPTION 'Cannot convert an unsupported quest objective type.';
                END IF;
            END $$;
            """
        );

        migrationBuilder.RenameColumn(
            name: "state_id",
            table: "quest_objectives",
            newName: "location_id"
        );

        migrationBuilder.AddColumn<Guid>(
            name: "building_id",
            table: "quest_objectives",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.AddColumn<Guid>(
            name: "city_id",
            table: "quest_objectives",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.AddColumn<int>(
            name: "collect_item_required_amount",
            table: "quest_objectives",
            type: "integer",
            nullable: true
        );

        migrationBuilder.AddColumn<Guid>(
            name: "creature_id",
            table: "quest_objectives",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "creature_type",
            table: "quest_objectives",
            type: "text",
            nullable: true
        );

        migrationBuilder.AddColumn<Guid>(
            name: "item_id",
            table: "quest_objectives",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.AddColumn<int>(
            name: "kill_creature_type_required_amount",
            table: "quest_objectives",
            type: "integer",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "objective_kind",
            table: "quest_objectives",
            type: "character varying(21)",
            maxLength: 21,
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.Sql(
            """
            UPDATE quest_objectives
            SET objective_kind = CASE
                    WHEN type = 'Kill' THEN 'KillCreature'
                    WHEN type = 'Collect' THEN 'CollectItem'
                    WHEN type = 'Explore' AND target_type = 'Building' THEN 'ExploreBuilding'
                    WHEN type = 'Explore' THEN 'ExploreCity'
                    WHEN type = 'Speak' THEN 'SpeakToCreature'
                END,
                creature_id = CASE
                    WHEN target_type = 'Creature' THEN target
                END,
                item_id = CASE
                    WHEN target_type = 'Item' THEN target
                END,
                building_id = CASE
                    WHEN target_type = 'Building' THEN target
                END,
                city_id = CASE
                    WHEN target_type = 'City' THEN target
                END,
                collect_item_required_amount = CASE
                    WHEN type = 'Collect' THEN COALESCE(amount, 1)
                END;
            """
        );

        migrationBuilder.DropColumn(name: "amount", table: "quest_objectives");
        migrationBuilder.DropColumn(name: "target", table: "quest_objectives");
        migrationBuilder.DropColumn(name: "target_type", table: "quest_objectives");
        migrationBuilder.DropColumn(name: "type", table: "quest_objectives");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM quest_objectives
                    WHERE objective_kind = 'KillCreatureType'
                ) THEN
                    RAISE EXCEPTION 'Cannot downgrade creature-type kill objectives.';
                END IF;
            END $$;
            """
        );

        migrationBuilder.AddColumn<int>(
            name: "amount",
            table: "quest_objectives",
            type: "integer",
            nullable: true
        );

        migrationBuilder.AddColumn<Guid>(
            name: "target",
            table: "quest_objectives",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty
        );

        migrationBuilder.AddColumn<string>(
            name: "target_type",
            table: "quest_objectives",
            type: "text",
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.AddColumn<string>(
            name: "type",
            table: "quest_objectives",
            type: "text",
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.Sql(
            """
            UPDATE quest_objectives
            SET type = CASE
                    WHEN objective_kind = 'KillCreature' THEN 'Kill'
                    WHEN objective_kind = 'CollectItem' THEN 'Collect'
                    WHEN objective_kind IN ('ExploreBuilding', 'ExploreCity') THEN 'Explore'
                    WHEN objective_kind = 'SpeakToCreature' THEN 'Speak'
                END,
                target_type = CASE
                    WHEN objective_kind IN ('KillCreature', 'SpeakToCreature') THEN 'Creature'
                    WHEN objective_kind = 'CollectItem' THEN 'Item'
                    WHEN objective_kind = 'ExploreBuilding' THEN 'Building'
                    WHEN objective_kind = 'ExploreCity' THEN 'City'
                END,
                target = CASE
                    WHEN objective_kind IN ('KillCreature', 'SpeakToCreature') THEN creature_id
                    WHEN objective_kind = 'CollectItem' THEN item_id
                    WHEN objective_kind = 'ExploreBuilding' THEN building_id
                    WHEN objective_kind = 'ExploreCity' THEN city_id
                END,
                amount = CASE
                    WHEN objective_kind = 'CollectItem' THEN collect_item_required_amount
                END;
            """
        );

        migrationBuilder.DropColumn(name: "building_id", table: "quest_objectives");
        migrationBuilder.DropColumn(name: "city_id", table: "quest_objectives");
        migrationBuilder.DropColumn(
            name: "collect_item_required_amount",
            table: "quest_objectives"
        );
        migrationBuilder.DropColumn(name: "creature_id", table: "quest_objectives");
        migrationBuilder.DropColumn(name: "creature_type", table: "quest_objectives");
        migrationBuilder.DropColumn(
            name: "kill_creature_type_required_amount",
            table: "quest_objectives"
        );
        migrationBuilder.DropColumn(name: "objective_kind", table: "quest_objectives");

        migrationBuilder.RenameColumn(
            name: "location_id",
            table: "quest_objectives",
            newName: "state_id"
        );
    }
}
