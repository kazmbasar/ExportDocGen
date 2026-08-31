using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExportDocGen.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerTaxIdAndPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Customers",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                table: "Customers",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TaxId",
                table: "Customers");
        }
    }
}
