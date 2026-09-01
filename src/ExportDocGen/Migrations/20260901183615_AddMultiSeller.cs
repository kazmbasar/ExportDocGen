using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExportDocGen.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiSeller : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentType",
                table: "Customers",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SellerCompanies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ProformaTemplate = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    NumberFormat = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    LetterheadPath = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    DefaultBankDetails = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultDeliveryTime = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    DefaultValidity = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CountryOfOrigin = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerCompanies", x => x.Id);
                });

            // Seed the two companies here so the required Orders.SellerCompanyId
            // FK below has a valid target for pre-existing orders (which back-fill
            // to Filtorq = Id 1). SeedData.EnsureSellerCompaniesAsync is a no-op
            // once these rows exist.
            migrationBuilder.InsertData(
                table: "SellerCompanies",
                columns: new[] { "Id", "Name", "ShortName", "ProformaTemplate", "NumberFormat", "LetterheadPath", "DefaultBankDetails", "DefaultDeliveryTime", "DefaultValidity", "CountryOfOrigin", "IsActive" },
                values: new object[,]
                {
                    {
                        1,
                        "Filtorq Filtre İthalat İhracat Sanayi ve Tic. A.Ş.",
                        "Filtorq",
                        "FiltorqClassic",
                        "ExpYearSeq",
                        "wwwroot/proforma-letterhead.png",
                        "Company Name : Filtorq Filtre İthalat İhracat Sanayi ve Tic. A.Ş.\nOur Bank : TÜRKİYE CUMHURİYETİ ZİRAAT BANKASI A.Ş.\nSwift Code : TCZBTR2AXXX\nIBAN NO : TR62 0001 0020 6383 4792 2750 06",
                        null,
                        null,
                        "Türkiye",
                        true
                    },
                    {
                        2,
                        "İkiler Otomotiv Filtre İthalat İhracat Sanayi ve Ticaret A.Ş.",
                        "İkiler",
                        "IkilerGrid",
                        "DateSlashSeq",
                        "wwwroot/ikiler-letterhead.png",
                        "Company Name : İkiler Otomotiv Filtre İthalat İhracat Sanayi ve Ticaret Anonim Şirketi\nOur Bank : ZİRAAT BANKASI\nBranch : Denizli Ticari\nBranch Code : 2142\nAccount No : 2063 3710 2798 5013\nSwift Code : TCZBTR2AXXX\nIBAN NO : TR 9200 0100 2063 3710 2798 5013",
                        "6 WEEKS",
                        "2 WEEKS FROM PROFORMA DATE",
                        "Türkiye",
                        true
                    },
                });

            migrationBuilder.AddColumn<string>(
                name: "BankDetails",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryTime",
                table: "Orders",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SellerCompanyId",
                table: "Orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Validity",
                table: "Orders",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SellerCompanyId",
                table: "Orders",
                column: "SellerCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_SellerCompanies_SellerCompanyId",
                table: "Orders",
                column: "SellerCompanyId",
                principalTable: "SellerCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_SellerCompanies_SellerCompanyId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "SellerCompanies");

            migrationBuilder.DropIndex(
                name: "IX_Orders_SellerCompanyId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BankDetails",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryTime",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SellerCompanyId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Validity",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "Customers");
        }
    }
}
