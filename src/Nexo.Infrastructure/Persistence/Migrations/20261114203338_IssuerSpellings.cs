using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IssuerSpellings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issuer_spellings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    issuer_document = table.Column<string>(type: "character(14)", fixedLength: true, maxLength: 14, nullable: false),
                    issuer_name = table.Column<string>(type: "text", nullable: false),
                    fund_id = table.Column<long>(type: "bigint", nullable: false),
                    fund_class_id = table.Column<long>(type: "bigint", nullable: true),
                    reference_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issuer_spellings", x => x.id);
                    table.ForeignKey(
                        name: "fk_issuer_spellings_fund_classes_fund_class_id",
                        column: x => x.fund_class_id,
                        principalTable: "fund_classes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_issuer_spellings_funds_fund_id",
                        column: x => x.fund_id,
                        principalTable: "funds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_issuer_spellings_fund_class_id",
                table: "issuer_spellings",
                column: "fund_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_issuer_spellings_fund_id_reference_date",
                table: "issuer_spellings",
                columns: new[] { "fund_id", "reference_date" });

            migrationBuilder.CreateIndex(
                name: "ix_issuer_spellings_issuer_document_fund_id_fund_class_id_refe",
                table: "issuer_spellings",
                columns: new[] { "issuer_document", "fund_id", "fund_class_id", "reference_date", "issuer_name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_issuer_spellings_issuer_name",
                table: "issuer_spellings",
                column: "issuer_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issuer_spellings");
        }
    }
}
