using Nexo.Application.Ingestion.Parsing;
using Nexo.Application.Ingestion.Parsing.Cda;
using Nexo.Application.Ingestion.Ports;
using Npgsql;
using NpgsqlTypes;

namespace Nexo.Infrastructure.Persistence.Loading;

/// <summary>Carga de um zip de carteiras: as linhas vão em lotes para tabelas temporárias
/// e só entram nas tabelas de verdade no final, numa única transação.</summary>
internal sealed class PortfolioLoadSession : IPortfolioLoadSession
{
    private const int BatchSize = 5_000;

    private readonly NpgsqlConnection _connection;
    private readonly long _datasetFileId;
    private readonly StagedRejections _rejections;
    private readonly List<PositionRecord> _positions = new(BatchSize);
    private readonly List<NetWorthRecord> _netWorths = [];

    private PortfolioLoadSession(NpgsqlConnection connection, long datasetFileId)
    {
        _connection = connection;
        _datasetFileId = datasetFileId;
        _rejections = new StagedRejections(connection);
    }

    public static async Task<PortfolioLoadSession> StartAsync(NpgsqlDataSource dataSource, long datasetFileId, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(PortfolioLoadSql.CreateStageTables + StagedRejections.CreateTableSql, cancellationToken);
        return new PortfolioLoadSession(connection, datasetFileId);
    }

    public async Task AddPositionAsync(PositionRecord position, CancellationToken cancellationToken)
    {
        _positions.Add(position);

        if (_positions.Count >= BatchSize)
            await FlushPositionsAsync(cancellationToken);
    }

    public async Task AddNetWorthAsync(NetWorthRecord netWorth, CancellationToken cancellationToken)
    {
        _netWorths.Add(netWorth);

        if (_netWorths.Count >= BatchSize)
            await FlushNetWorthsAsync(cancellationToken);
    }

    public Task AddRejectionAsync(string fileName, RowRejection rejection, CancellationToken cancellationToken) =>
        _rejections.AddAsync(fileName, rejection, cancellationToken);

    public async Task<int> CompleteAsync(CancellationToken cancellationToken)
    {
        await FlushPositionsAsync(cancellationToken);
        await FlushNetWorthsAsync(cancellationToken);
        await _rejections.FlushAsync(cancellationToken);
        await _connection.ExecuteAsync(PortfolioLoadSql.IndexStageTables, cancellationToken);

        // Criar a partição fora da transação faz o bloqueio da tabela durar só um instante.
        await _connection.ExecuteAsync(PortfolioLoadSql.EnsureMonthlyPartitions, cancellationToken);

        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);

        await _connection.ExecuteAsync(PortfolioLoadSql.LockSameSourceFile, _datasetFileId, cancellationToken);
        await _connection.ExecuteAsync(StagedRejections.MoveToFinalTableSql, _datasetFileId, cancellationToken);
        await _connection.ExecuteAsync(PortfolioLoadSql.BuildReporters, cancellationToken);
        await _connection.ExecuteAsync(PortfolioLoadSql.CreatePlaceholderFunds, cancellationToken);
        await _connection.ExecuteAsync(PortfolioLoadSql.ResolveFunds, cancellationToken);
        await _connection.ExecuteAsync(PortfolioLoadSql.MarkChangedReporters, cancellationToken);
        await _connection.ExecuteAsync(PortfolioLoadSql.CreateRevisions, _datasetFileId, cancellationToken);
        await _connection.ExecuteAsync(PortfolioLoadSql.InsertNewAssets, cancellationToken);
        await _connection.ExecuteAsync(PortfolioLoadSql.InsertPositions, cancellationToken);
        var changed = await _connection.ScalarAsync<int>(PortfolioLoadSql.CountChangedReporters, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return changed;
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    private async Task FlushPositionsAsync(CancellationToken cancellationToken)
    {
        if (_positions.Count == 0)
            return;

        await using var importer = await _connection.BeginBinaryImportAsync(PortfolioLoadSql.CopyPositions, cancellationToken);

        foreach (var position in _positions)
        {
            await importer.StartRowAsync(cancellationToken);
            await WriteFundAsync(importer, position.Fund, cancellationToken);
            await WritePositionAsync(importer, position, cancellationToken);
            await WriteAssetAsync(importer, position.Asset, cancellationToken);
        }

        await importer.CompleteAsync(cancellationToken);
        _positions.Clear();
    }

    private async Task FlushNetWorthsAsync(CancellationToken cancellationToken)
    {
        if (_netWorths.Count == 0)
            return;

        await using var importer = await _connection.BeginBinaryImportAsync(PortfolioLoadSql.CopyNetWorths, cancellationToken);

        foreach (var netWorth in _netWorths)
        {
            await importer.StartRowAsync(cancellationToken);
            await WriteFundAsync(importer, netWorth.Fund, cancellationToken);
            await importer.WriteNullableAsync(netWorth.NetWorth, NpgsqlDbType.Numeric, cancellationToken);
            await importer.WriteAsync(netWorth.Origin.RowHash, NpgsqlDbType.Char, cancellationToken);
        }

        await importer.CompleteAsync(cancellationToken);
        _netWorths.Clear();
    }

    private static async Task WriteFundAsync(NpgsqlBinaryImporter importer, ReportingFund fund, CancellationToken cancellationToken)
    {
        await importer.WriteAsync(fund.Cnpj.Value, NpgsqlDbType.Char, cancellationToken);
        await importer.WriteAsync(fund.Name, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(fund.FundType, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(fund.ReferenceDate, NpgsqlDbType.Date, cancellationToken);
    }

    private static async Task WritePositionAsync(NpgsqlBinaryImporter importer, PositionRecord position, CancellationToken cancellationToken)
    {
        await importer.WriteNullableAsync((short?)position.Block, NpgsqlDbType.Smallint, cancellationToken);
        await importer.WriteAsync(position.IsConfidential, NpgsqlDbType.Boolean, cancellationToken);
        await importer.WriteAsync(position.ApplicationType, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(position.AssetType, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(position.TradingIntent, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(position.IssuerIsRelated, NpgsqlDbType.Boolean, cancellationToken);

        var amounts = position.Amounts;
        await importer.WriteNullableAsync(amounts.Quantity, NpgsqlDbType.Numeric, cancellationToken);
        await importer.WriteNullableAsync(amounts.MarketValue, NpgsqlDbType.Numeric, cancellationToken);
        await importer.WriteNullableAsync(amounts.CostValue, NpgsqlDbType.Numeric, cancellationToken);
        await importer.WriteNullableAsync(amounts.BoughtQuantity, NpgsqlDbType.Numeric, cancellationToken);
        await importer.WriteNullableAsync(amounts.BoughtValue, NpgsqlDbType.Numeric, cancellationToken);
        await importer.WriteNullableAsync(amounts.SoldQuantity, NpgsqlDbType.Numeric, cancellationToken);
        await importer.WriteNullableAsync(amounts.SoldValue, NpgsqlDbType.Numeric, cancellationToken);

        await importer.WriteNullableAsync(position.ConfidentialUntil, NpgsqlDbType.Date, cancellationToken);
        await importer.WriteNullableAsync(position.DetailsJson, NpgsqlDbType.Jsonb, cancellationToken);
        await importer.WriteAsync(position.Origin.RowHash, NpgsqlDbType.Char, cancellationToken);
    }

    private static async Task WriteAssetAsync(NpgsqlBinaryImporter importer, AssetRecord? asset, CancellationToken cancellationToken)
    {
        await importer.WriteNullableAsync(asset?.Identity.Key, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync((short?)asset?.Block, NpgsqlDbType.Smallint, cancellationToken);
        await importer.WriteNullableAsync(asset?.ApplicationType, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(asset?.AssetType, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(asset?.Isin, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(asset?.Ticker, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(asset?.Description, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(asset?.IssuerDocument, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(asset?.IssuerName, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(asset?.CountryCode, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(asset?.MaturityDate, NpgsqlDbType.Date, cancellationToken);
        await importer.WriteNullableAsync(asset?.InvestedFundDocument, NpgsqlDbType.Text, cancellationToken);
    }
}
