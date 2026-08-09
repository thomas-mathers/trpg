using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConvertShieldResistancesToAttributeModifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE items
                SET modifiers = COALESCE(modifiers, '[]'::jsonb) || jsonb_build_array(
                    jsonb_build_object('$type', 'attribute', 'Amount', magic_resistance, 'Attribute', 'MagicResistance', 'AmountType', 'Flat', 'MinItemLevel', 0),
                    jsonb_build_object('$type', 'attribute', 'Amount', fire_resistance, 'Attribute', 'FireResistance', 'AmountType', 'Flat', 'MinItemLevel', 0),
                    jsonb_build_object('$type', 'attribute', 'Amount', ice_resistance, 'Attribute', 'IceResistance', 'AmountType', 'Flat', 'MinItemLevel', 0),
                    jsonb_build_object('$type', 'attribute', 'Amount', lightning_resistance, 'Attribute', 'LightningResistance', 'AmountType', 'Flat', 'MinItemLevel', 0),
                    jsonb_build_object('$type', 'attribute', 'Amount', poison_resistance, 'Attribute', 'PoisonResistance', 'AmountType', 'Flat', 'MinItemLevel', 0)
                )
                WHERE item_type = 'shield';
                """
            );

            migrationBuilder.DropColumn(name: "fire_resistance", table: "items");

            migrationBuilder.DropColumn(name: "ice_resistance", table: "items");

            migrationBuilder.DropColumn(name: "lightning_resistance", table: "items");

            migrationBuilder.DropColumn(name: "magic_resistance", table: "items");

            migrationBuilder.DropColumn(name: "poison_resistance", table: "items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "fire_resistance",
                table: "items",
                type: "real",
                nullable: true
            );

            migrationBuilder.AddColumn<float>(
                name: "ice_resistance",
                table: "items",
                type: "real",
                nullable: true
            );

            migrationBuilder.AddColumn<float>(
                name: "lightning_resistance",
                table: "items",
                type: "real",
                nullable: true
            );

            migrationBuilder.AddColumn<float>(
                name: "magic_resistance",
                table: "items",
                type: "real",
                nullable: true
            );

            migrationBuilder.AddColumn<float>(
                name: "poison_resistance",
                table: "items",
                type: "real",
                nullable: true
            );
        }
    }
}
