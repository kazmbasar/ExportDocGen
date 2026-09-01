using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExportDocGen.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerExporterCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing customers pre-date the two-company model — back-fill to
            // Filtorq (SellerCompanies.Id 1, seeded by AddMultiSeller).
            migrationBuilder.AddColumn<int>(
                name: "SellerCompanyId",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_SellerCompanyId",
                table: "Customers",
                column: "SellerCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_SellerCompanies_SellerCompanyId",
                table: "Customers",
                column: "SellerCompanyId",
                principalTable: "SellerCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_SellerCompanies_SellerCompanyId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_SellerCompanyId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SellerCompanyId",
                table: "Customers");
        }
    }
}
