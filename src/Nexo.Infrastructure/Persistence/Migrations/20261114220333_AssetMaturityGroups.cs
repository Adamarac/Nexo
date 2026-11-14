using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssetMaturityGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "maturity_group",
                table: "assets",
                type: "text",
                nullable: true,
                computedColumnSql: "CASE\n    WHEN maturity_date IS NULL OR ticker IS NOT NULL OR block NOT IN (0, 1, 5, 6) THEN NULL\n    WHEN block = 1 THEN 'bond|' || coalesce(description, '')\n    WHEN issuer_document IS NOT NULL THEN 'issuer|' || coalesce(application_type, '') || '|' || coalesce(asset_type, '') || '|' || issuer_document\n    ELSE 'name|' || coalesce(application_type, '') || '|' || coalesce(description, '')\nEND",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_assets_maturity_group",
                table: "assets",
                column: "maturity_group");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_assets_maturity_group",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "maturity_group",
                table: "assets");
        }
    }
}
