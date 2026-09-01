using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExportDocGen.Migrations
{
    /// <inheritdoc />
    public partial class AddStockCatalogFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CartonHeightCm",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CartonLengthCm",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CartonTareWeightKg",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UnitsPerCarton",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "CartonWidthCm",
                table: "Products",
                newName: "UnitVolumeM3");

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "Products",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "Products",
                type: "TEXT",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Brand",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "UnitVolumeM3",
                table: "Products",
                newName: "CartonWidthCm");

            migrationBuilder.AddColumn<decimal>(
                name: "CartonHeightCm",
                table: "Products",
                type: "TEXT",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CartonLengthCm",
                table: "Products",
                type: "TEXT",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CartonTareWeightKg",
                table: "Products",
                type: "TEXT",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "UnitsPerCarton",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
