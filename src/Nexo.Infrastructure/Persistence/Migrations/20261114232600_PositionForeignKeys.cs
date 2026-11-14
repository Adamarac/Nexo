using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <summary>Cada partição de posições ganha chaves estrangeiras para carteira e ativo.</summary>
    /// <remarks>As partições que já existem são conferidas aqui; as novas, pela carga de cada mês.</remarks>
    public partial class PositionForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PartitionedPositionsSql.CreateForeignKeysFunction);
            migrationBuilder.Sql(PartitionedPositionsSql.AddForeignKeysToExistingPartitions);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PartitionedPositionsSql.DropForeignKeys);
        }
    }
}
