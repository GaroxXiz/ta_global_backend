using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TAGlobal.Api.Data.Migrations;

public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Categories",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Categories", category => category.Id));

        migrationBuilder.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                Stock = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CategoryId = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", product => product.Id);
                table.ForeignKey("FK_Products_Categories_CategoryId", product => product.CategoryId,
                    "Categories", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_Categories_Name", "Categories", "Name", unique: true);
        migrationBuilder.CreateIndex("IX_Products_CategoryId", "Products", "CategoryId");
        migrationBuilder.CreateIndex("IX_Products_Name", "Products", "Name");

        migrationBuilder.InsertData("Categories", new[] { "Id", "Name", "Description" }, new object[,]
        {
            { 1, "Fashion Wanita", "Dress, blus, gamis, rok, dan outerwear" },
            { 2, "Fashion Pria", "Kemeja, koko, celana, dan jaket" },
            { 3, "Hijab & Kerudung", "Pashmina, hijab instan, segi empat, dan inner" },
            { 4, "Kain & Tekstil", "Batik, katun, sutra, renda, dan linen" },
            { 5, "Anak & Bayi", "Pakaian anak dan bayi" },
            { 6, "Tas & Aksesori", "Tas dan aksesori" },
            { 7, "Perlengkapan Ibadah", "Mukena, sarung, peci, dan sajadah" },
            { 8, "Paket Grosir", "Paket grosir, lot, dan overstock" }
        });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Products");
        migrationBuilder.DropTable("Categories");
    }
}

