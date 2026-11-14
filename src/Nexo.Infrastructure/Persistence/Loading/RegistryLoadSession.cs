using Microsoft.Extensions.Logging;
using Nexo.Application.Ingestion.Parsing;
using Nexo.Application.Ingestion.Parsing.Registry;
using Nexo.Application.Ingestion.Ports;
using Npgsql;
using NpgsqlTypes;

namespace Nexo.Infrastructure.Persistence.Loading;

/// <summary>Carga do cadastro: os registros vão para tabelas temporárias e depois são mesclados de uma vez.</summary>
internal sealed class RegistryLoadSession : IRegistryLoadSession
{
    private const int BatchSize = 5_000;

    private readonly NpgsqlConnection _connection;
    private readonly long _datasetFileId;
    private readonly ILogger _logger;
    private readonly StagedRejections _rejections;
    private readonly List<RegistryFundRecord> _funds = new(BatchSize);
    private readonly List<RegistryClassRecord> _classes = new(BatchSize);

    private RegistryLoadSession(NpgsqlConnection connection, long datasetFileId, ILogger logger)
    {
        _connection = connection;
        _datasetFileId = datasetFileId;
        _logger = logger;
        _rejections = new StagedRejections(connection);
    }

    public static async Task<RegistryLoadSession> StartAsync(
        NpgsqlDataSource dataSource, long datasetFileId, ILogger logger, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(RegistryLoadSql.CreateStageTables + StagedRejections.CreateTableSql, cancellationToken);
        return new RegistryLoadSession(connection, datasetFileId, logger);
    }

    public async Task AddFundAsync(RegistryFundRecord fund, CancellationToken cancellationToken)
    {
        _funds.Add(fund);

        if (_funds.Count >= BatchSize)
            await FlushFundsAsync(cancellationToken);
    }

    public async Task AddClassAsync(RegistryClassRecord fundClass, CancellationToken cancellationToken)
    {
        _classes.Add(fundClass);

        if (_classes.Count >= BatchSize)
            await FlushClassesAsync(cancellationToken);
    }

    public Task AddRejectionAsync(string fileName, RowRejection rejection, CancellationToken cancellationToken) =>
        _rejections.AddAsync(fileName, rejection, cancellationToken);

    public async Task<int> CompleteAsync(CancellationToken cancellationToken)
    {
        await FlushFundsAsync(cancellationToken);
        await FlushClassesAsync(cancellationToken);
        await _rejections.FlushAsync(cancellationToken);

        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);

        await _connection.ExecuteAsync(StagedRejections.MoveToFinalTableSql, _datasetFileId, cancellationToken);
        var fundsChanged = await _connection.ExecuteCountAsync(RegistryLoadSql.UpsertFunds, cancellationToken);
        var classesChanged = await _connection.ExecuteCountAsync(RegistryLoadSql.UpsertClasses, cancellationToken);
        await _connection.ExecuteAsync(RegistryLoadSql.AttachEarlyPortfolios, cancellationToken);
        var classesWithoutFund = await _connection.ScalarAsync<int>(RegistryLoadSql.CountClassesWithoutFund, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        if (classesWithoutFund > 0)
            _logger.LogWarning("{Count} classes do cadastro apontam para um registro de fundo que não existe no arquivo", classesWithoutFund);

        return fundsChanged + classesChanged;
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    private async Task FlushFundsAsync(CancellationToken cancellationToken)
    {
        if (_funds.Count == 0)
            return;

        await using var importer = await _connection.BeginBinaryImportAsync(RegistryLoadSql.CopyFunds, cancellationToken);

        foreach (var fund in _funds)
        {
            await importer.StartRowAsync(cancellationToken);
            await importer.WriteAsync(fund.RegistryId, NpgsqlDbType.Bigint, cancellationToken);
            await importer.WriteAsync(fund.Cnpj.Value, NpgsqlDbType.Char, cancellationToken);
            await importer.WriteAsync(fund.Name, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteNullableAsync(fund.RegisteredAt, NpgsqlDbType.Date, cancellationToken);
            await importer.WriteNullableAsync(fund.FundType, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteNullableAsync(fund.Situation, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteNullableAsync(fund.AdministratorName, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteNullableAsync(fund.ManagerName, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteNullableAsync(fund.NetWorth, NpgsqlDbType.Numeric, cancellationToken);
            await importer.WriteNullableAsync(fund.NetWorthDate, NpgsqlDbType.Date, cancellationToken);
        }

        await importer.CompleteAsync(cancellationToken);
        _funds.Clear();
    }

    private async Task FlushClassesAsync(CancellationToken cancellationToken)
    {
        if (_classes.Count == 0)
            return;

        await using var importer = await _connection.BeginBinaryImportAsync(RegistryLoadSql.CopyClasses, cancellationToken);

        foreach (var fundClass in _classes)
        {
            await importer.StartRowAsync(cancellationToken);
            await importer.WriteAsync(fundClass.FundRegistryId, NpgsqlDbType.Bigint, cancellationToken);
            await importer.WriteAsync(fundClass.Cnpj.Value, NpgsqlDbType.Char, cancellationToken);
            await importer.WriteAsync(fundClass.Name, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteNullableAsync(fundClass.RegisteredAt, NpgsqlDbType.Date, cancellationToken);
            await importer.WriteNullableAsync(fundClass.ClassType, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteNullableAsync(fundClass.Situation, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteNullableAsync(fundClass.AnbimaClassification, NpgsqlDbType.Text, cancellationToken);
            await importer.WriteNullableAsync(fundClass.TargetAudience, NpgsqlDbType.Text, cancellationToken);
        }

        await importer.CompleteAsync(cancellationToken);
        _classes.Clear();
    }
}
