using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "assets",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    identity_key = table.Column<string>(type: "text", nullable: false),
                    block = table.Column<short>(type: "smallint", nullable: false),
                    application_type = table.Column<string>(type: "text", nullable: true),
                    asset_type = table.Column<string>(type: "text", nullable: true),
                    isin = table.Column<string>(type: "text", nullable: true),
                    ticker = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    issuer_document = table.Column<string>(type: "text", nullable: true),
                    issuer_name = table.Column<string>(type: "text", nullable: true),
                    country_code = table.Column<string>(type: "text", nullable: true),
                    maturity_date = table.Column<DateOnly>(type: "date", nullable: true),
                    invested_fund_document = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dataset_files",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_key = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    e_tag = table.Column<string>(type: "text", nullable: true),
                    last_modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    content_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    rows_read = table.Column<int>(type: "integer", nullable: false),
                    rows_accepted = table.Column<int>(type: "integer", nullable: false),
                    rows_rejected = table.Column<int>(type: "integer", nullable: false),
                    portfolios_changed = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    discovered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processing_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dataset_files", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "funds",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cnpj = table.Column<string>(type: "character(14)", fixedLength: true, maxLength: 14, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    fund_type = table.Column<string>(type: "text", nullable: true),
                    situation = table.Column<string>(type: "text", nullable: true),
                    administrator_name = table.Column<string>(type: "text", nullable: true),
                    manager_name = table.Column<string>(type: "text", nullable: true),
                    net_worth = table.Column<decimal>(type: "numeric", nullable: true),
                    net_worth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_placeholder = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_funds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    routing_key = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    correlation_id = table.Column<string>(type: "text", nullable: false),
                    trace_parent = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rejected_rows",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    dataset_file_id = table.Column<long>(type: "bigint", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    detail = table.Column<string>(type: "text", nullable: false),
                    raw_line = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rejected_rows", x => x.id);
                    table.ForeignKey(
                        name: "fk_rejected_rows_dataset_files_dataset_file_id",
                        column: x => x.dataset_file_id,
                        principalTable: "dataset_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fund_classes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fund_id = table.Column<long>(type: "bigint", nullable: false),
                    cnpj = table.Column<string>(type: "character(14)", fixedLength: true, maxLength: 14, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    class_type = table.Column<string>(type: "text", nullable: true),
                    situation = table.Column<string>(type: "text", nullable: true),
                    anbima_classification = table.Column<string>(type: "text", nullable: true),
                    target_audience = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fund_classes", x => x.id);
                    table.ForeignKey(
                        name: "fk_fund_classes_funds_fund_id",
                        column: x => x.fund_id,
                        principalTable: "funds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "portfolios",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fund_id = table.Column<long>(type: "bigint", nullable: false),
                    fund_class_id = table.Column<long>(type: "bigint", nullable: true),
                    reference_date = table.Column<DateOnly>(type: "date", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    net_worth = table.Column<decimal>(type: "numeric", nullable: true),
                    fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    dataset_file_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolios", x => x.id);
                    table.ForeignKey(
                        name: "fk_portfolios_dataset_files_dataset_file_id",
                        column: x => x.dataset_file_id,
                        principalTable: "dataset_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_portfolios_fund_classes_fund_class_id",
                        column: x => x.fund_class_id,
                        principalTable: "fund_classes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_portfolios_funds_fund_id",
                        column: x => x.fund_id,
                        principalTable: "funds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Tabela particionada por mês: o SQL fica em PartitionedPositionsSql porque o EF não gera partições.
            migrationBuilder.Sql(PartitionedPositionsSql.CreateTable);
            migrationBuilder.Sql(PartitionedPositionsSql.CreatePartitionFunction);

            migrationBuilder.CreateIndex(
                name: "ix_assets_description",
                table: "assets",
                column: "description")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_assets_identity_key",
                table: "assets",
                column: "identity_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assets_isin",
                table: "assets",
                column: "isin");

            migrationBuilder.CreateIndex(
                name: "ix_assets_ticker",
                table: "assets",
                column: "ticker");

            migrationBuilder.CreateIndex(
                name: "ix_dataset_files_source_key_content_hash",
                table: "dataset_files",
                columns: new[] { "source_key", "content_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dataset_files_source_key_discovered_at",
                table: "dataset_files",
                columns: new[] { "source_key", "discovered_at" });

            migrationBuilder.CreateIndex(
                name: "ix_dataset_files_status",
                table: "dataset_files",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_fund_classes_cnpj",
                table: "fund_classes",
                column: "cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fund_classes_fund_id",
                table: "fund_classes",
                column: "fund_id");

            migrationBuilder.CreateIndex(
                name: "ix_funds_cnpj",
                table: "funds",
                column: "cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_funds_fund_type_situation",
                table: "funds",
                columns: new[] { "fund_type", "situation" });

            migrationBuilder.CreateIndex(
                name: "ix_funds_name",
                table: "funds",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_message_id",
                table: "outbox_messages",
                column: "message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                table: "outbox_messages",
                column: "created_at",
                filter: "sent_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_portfolios_current",
                table: "portfolios",
                columns: new[] { "fund_id", "fund_class_id", "reference_date" },
                unique: true,
                filter: "is_current")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_portfolios_dataset_file_id",
                table: "portfolios",
                column: "dataset_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_portfolios_fund_class_id",
                table: "portfolios",
                column: "fund_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_portfolios_fund_id_fund_class_id_reference_date_revision",
                table: "portfolios",
                columns: new[] { "fund_id", "fund_class_id", "reference_date", "revision" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_portfolios_reference_date_is_current",
                table: "portfolios",
                columns: new[] { "reference_date", "is_current" });

            migrationBuilder.CreateIndex(
                name: "ix_positions_asset_id_reference_date",
                table: "positions",
                columns: new[] { "asset_id", "reference_date" });

            migrationBuilder.CreateIndex(
                name: "ix_positions_portfolio_id",
                table: "positions",
                column: "portfolio_id");

            migrationBuilder.CreateIndex(
                name: "ix_rejected_rows_dataset_file_id_reason",
                table: "rejected_rows",
                columns: new[] { "dataset_file_id", "reason" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "positions");

            migrationBuilder.Sql(PartitionedPositionsSql.DropPartitionFunction);

            migrationBuilder.DropTable(
                name: "rejected_rows");

            migrationBuilder.DropTable(
                name: "assets");

            migrationBuilder.DropTable(
                name: "portfolios");

            migrationBuilder.DropTable(
                name: "dataset_files");

            migrationBuilder.DropTable(
                name: "fund_classes");

            migrationBuilder.DropTable(
                name: "funds");
        }
    }
}
