using Microsoft.EntityFrameworkCore;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Assets;
using Nexo.Domain.Assets;
using Nexo.Domain.Portfolios;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Queries;

internal sealed class AssetQueries(NexoDbContext db) : IAssetQueries
{
    public async Task<PagedResult<AssetView>> SearchAsync(AssetSearch search, CancellationToken cancellationToken)
    {
        var assets = db.Assets.AsNoTracking();

        if (search.Block is not null)
            assets = assets.Where(asset => asset.Block == (CdaBlock)search.Block.Value);

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var code = search.Search.Trim().ToUpperInvariant();
            var pattern = LikePattern.Contains(search.Search);
            assets = assets.Where(asset => asset.Ticker == code || asset.Isin == code || EF.Functions.ILike(asset.Description!, pattern));
        }

        var total = await assets.CountAsync(cancellationToken);
        var items = await assets
            .OrderBy(asset => asset.Description)
            .ThenBy(asset => asset.Id)
            .Skip(search.Skip)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AssetView>(items.Select(ToView).ToList(), search.Page, search.PageSize, total);
    }

    public async Task<AssetView?> GetAsync(long assetId, CancellationToken cancellationToken)
    {
        var asset = await db.Assets.AsNoTracking().SingleOrDefaultAsync(asset => asset.Id == assetId, cancellationToken);
        return asset is null ? null : ToView(asset);
    }

    public async Task<PagedResult<AssetHolder>> GetHoldersAsync(long assetId, HolderSearch search, CancellationToken cancellationToken)
    {
        var referenceDate = search.ReferenceDate ?? await db.Positions
            .Where(position => position.AssetId == assetId && position.Portfolio.IsCurrent)
            .MaxAsync(position => (DateOnly?)position.ReferenceDate, cancellationToken);

        if (referenceDate is null)
            return new PagedResult<AssetHolder>([], search.Page, search.PageSize, 0);

        // Um fundo pode ter o mesmo ativo em mais de uma linha (para negociação e até o vencimento), então somamos.
        var holders = db.Positions
            .Where(position => position.AssetId == assetId && position.ReferenceDate == referenceDate && position.Portfolio.IsCurrent)
            .GroupBy(position => new { position.PortfolioId, position.Portfolio.FundId, position.Portfolio.Fund.Name, position.Portfolio.Fund.Cnpj })
            .Select(group => new
            {
                group.Key.PortfolioId,
                group.Key.FundId,
                group.Key.Name,
                group.Key.Cnpj,
                Quantity = group.Sum(position => position.Quantity),
                MarketValue = group.Sum(position => position.MarketValue),
                Share = group.Sum(position => position.ShareOfNetWorth),
            });

        var total = await holders.CountAsync(cancellationToken);
        var page = await holders
            .OrderByDescending(holder => holder.MarketValue ?? 0)
            .ThenBy(holder => holder.FundId)
            .Skip(search.Skip)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);

        var items = page
            .Select(holder => new AssetHolder(
                holder.FundId, holder.Cnpj.Value, holder.Name, holder.PortfolioId, referenceDate.Value,
                holder.Quantity, holder.MarketValue, holder.Share))
            .ToList();

        return new PagedResult<AssetHolder>(items, search.Page, search.PageSize, total);
    }

    // Caixa, valores a pagar e afins (bloco 8) ficam de fora: não são investimentos em um ativo.
    public async Task<ConcentrationReport> GetConcentrationAsync(ConcentrationSearch search, CancellationToken cancellationToken)
    {
        var referenceDate = search.ReferenceDate ?? await db.Portfolios
            .Where(portfolio => portfolio.IsCurrent)
            .MaxAsync(portfolio => (DateOnly?)portfolio.ReferenceDate, cancellationToken);

        if (referenceDate is null)
            return new ConcentrationReport(null, []);

        if (search.Grouped)
            return new ConcentrationReport(referenceDate, await GetGroupedConcentrationAsync(referenceDate.Value, search.Top, cancellationToken));

        var top = await db.Positions
            .Where(position => position.ReferenceDate == referenceDate
                && position.AssetId != null
                && !position.IsConfidential
                && position.Block != CdaBlock.Other
                && position.Portfolio.IsCurrent)
            .GroupBy(position => position.AssetId!.Value)
            .Select(group => new
            {
                AssetId = group.Key,
                Funds = group.Select(position => position.PortfolioId).Distinct().Count(),
                Total = group.Sum(position => position.MarketValue) ?? 0,
            })
            .OrderByDescending(item => item.Total)
            .Take(search.Top)
            .ToListAsync(cancellationToken);

        var assetIds = top.Select(item => item.AssetId).ToList();
        var assets = await db.Assets.AsNoTracking().Where(asset => assetIds.Contains(asset.Id)).ToDictionaryAsync(asset => asset.Id, cancellationToken);

        var items = top
            .Select(item => new ConcentrationItem(
                item.AssetId, assets[item.AssetId].Description, assets[item.AssetId].Ticker, assets[item.AssetId].Isin,
                assets[item.AssetId].ApplicationType, assets[item.AssetId].MaturityDate, item.Funds, item.Total))
            .ToList();

        return new ConcentrationReport(referenceDate, items);
    }

    // Primeiro filtra os ativos, depois junta os que caíram no mesmo grupo: buscar um ISIN traz só aquele vencimento.
    // Títulos com vários vencimentos vêm primeiro; depois, em ordem alfabética.
    private const string GroupsSql = """
        WITH matched AS (
            SELECT id, maturity_group, maturity_date, description
            FROM assets
            WHERE (@block::smallint IS NULL OR block = @block)
              AND (@pattern::text IS NULL OR ticker = @code OR isin = @code OR description ILIKE @pattern)
        ),
        groups AS (
            SELECT min(id) AS asset_id,
                   count(*)::int AS assets,
                   min(maturity_date) AS first_maturity,
                   max(maturity_date) AS last_maturity,
                   min(regexp_replace(description, '^[^[:alnum:]]+', '')) AS sort_name
            FROM matched
            GROUP BY coalesce(maturity_group, 'asset|' || id)
        )
        SELECT g.asset_id, g.assets, g.first_maturity, g.last_maturity, a.block, a.application_type, a.asset_type,
               a.issuer_name, a.issuer_document, a.isin, a.ticker, a.description, a.maturity_group, count(*) OVER ()::int
        FROM groups g
        JOIN assets a ON a.id = g.asset_id
        ORDER BY (g.assets > 1) DESC, g.sort_name NULLS LAST, g.asset_id
        LIMIT @take OFFSET @skip
        """;

    // Igual à concentração por ativo, mas somando todos os vencimentos de um mesmo título.
    private const string GroupedConcentrationSql = """
        SELECT min(a.id) AS asset_id,
               count(DISTINCT p.portfolio_id)::int AS funds,
               coalesce(sum(p.market_value), 0) AS total,
               count(DISTINCT a.id)::int AS maturities
        FROM positions p
        JOIN portfolios pf ON pf.id = p.portfolio_id AND pf.is_current
        JOIN assets a ON a.id = p.asset_id
        WHERE p.reference_date = @referenceDate
          AND NOT p.is_confidential
          AND p.block IS DISTINCT FROM 8
        GROUP BY coalesce(a.maturity_group, 'asset|' || a.id)
        ORDER BY total DESC, asset_id
        LIMIT @top
        """;

    private const string IssuerGroupPrefix = "issuer|";

    public async Task<PagedResult<AssetGroup>> SearchGroupsAsync(AssetSearch search, CancellationToken cancellationToken)
    {
        var hasSearch = !string.IsNullOrWhiteSpace(search.Search);
        var total = 0;
        var rows = await db.ReadAsync(
            GroupsSql,
            command =>
            {
                command.Add("block", search.Block is null ? null : (short)search.Block.Value);
                command.Add("code", hasSearch ? search.Search!.Trim().ToUpperInvariant() : null);
                command.Add("pattern", hasSearch ? LikePattern.Contains(search.Search!) : null);
                command.Add("take", search.PageSize);
                command.Add("skip", search.Skip);
            },
            reader =>
            {
                total = reader.GetInt32(13);
                return new GroupRow(
                    reader.GetInt64(0), reader.GetInt32(1), reader.NullableDate(2), reader.NullableDate(3), reader.GetInt16(4),
                    reader.NullableString(5), reader.NullableString(6), reader.NullableString(7), reader.NullableString(8),
                    reader.NullableString(9), reader.NullableString(10), reader.NullableString(11), reader.NullableString(12));
            },
            cancellationToken);

        var issuerNames = await db.MostUsedAsync(rows.Select(row => row.IssuerDocument), cancellationToken);
        var items = rows
            .Select(row => new AssetGroup(
                row.AssetId,
                GroupName(row.MaturityGroup, row.Description, row.AssetType, row.IssuerDocument, row.IssuerName, issuerNames),
                row.Block,
                row.ApplicationType,
                row.AssetType,
                IssuerName(row.IssuerDocument, row.IssuerName, issuerNames),
                row.Assets == 1 ? row.Isin : null,
                row.Assets == 1 ? row.Ticker : null,
                row.Assets,
                row.FirstMaturity,
                row.LastMaturity))
            .ToList();

        return new PagedResult<AssetGroup>(items, search.Page, search.PageSize, total);
    }

    public async Task<AssetGroupDetail?> GetMaturitiesAsync(long assetId, CancellationToken cancellationToken)
    {
        var asset = await db.Assets.AsNoTracking().SingleOrDefaultAsync(asset => asset.Id == assetId, cancellationToken);
        if (asset is null)
            return null;

        var members = asset.MaturityGroup is null
            ? [asset]
            : await db.Assets.AsNoTracking()
                .Where(member => member.MaturityGroup == asset.MaturityGroup)
                .OrderBy(member => member.MaturityDate)
                .ThenBy(member => member.Id)
                .ToListAsync(cancellationToken);

        var issuerNames = await db.MostUsedAsync([asset.IssuerDocument], cancellationToken);
        var name = GroupName(asset.MaturityGroup, asset.Description, asset.AssetType, asset.IssuerDocument, asset.IssuerName, issuerNames);
        return new AssetGroupDetail(asset.Id, name, members.Select(ToView).ToList());
    }

    private async Task<List<ConcentrationItem>> GetGroupedConcentrationAsync(
        DateOnly referenceDate, int top, CancellationToken cancellationToken)
    {
        var totals = await db.ReadAsync(
            GroupedConcentrationSql,
            command =>
            {
                command.Add("referenceDate", referenceDate);
                command.Add("top", top);
            },
            reader => (AssetId: reader.GetInt64(0), Funds: reader.GetInt32(1), Total: reader.GetDecimal(2), Maturities: reader.GetInt32(3)),
            cancellationToken);

        var assetIds = totals.Select(item => item.AssetId).ToList();
        var assets = await db.Assets.AsNoTracking().Where(asset => assetIds.Contains(asset.Id)).ToDictionaryAsync(asset => asset.Id, cancellationToken);
        var issuerNames = await db.MostUsedAsync(assets.Values.Select(asset => asset.IssuerDocument), cancellationToken);

        return totals
            .Select(item =>
            {
                var asset = assets[item.AssetId];
                var single = item.Maturities == 1;
                var name = single
                    ? asset.Description
                    : GroupName(asset.MaturityGroup, asset.Description, asset.AssetType, asset.IssuerDocument, asset.IssuerName, issuerNames);
                return new ConcentrationItem(
                    asset.Id, name, single ? asset.Ticker : null, single ? asset.Isin : null, asset.ApplicationType,
                    single ? asset.MaturityDate : null, item.Funds, item.Total, item.Maturities);
            })
            .ToList();
    }

    // Grupo de emissor leva o tipo do papel e o nome mais usado do CNPJ; os outros usam a própria descrição.
    private static string? GroupName(
        string? maturityGroup, string? description, string? assetType, string? issuerDocument, string? issuerName,
        Dictionary<string, string> issuerNames)
    {
        if (maturityGroup is null || !maturityGroup.StartsWith(IssuerGroupPrefix, StringComparison.Ordinal))
            return description;

        var issuer = IssuerName(issuerDocument, issuerName, issuerNames);
        return string.IsNullOrWhiteSpace(assetType) ? issuer : $"{assetType} · {issuer}";
    }

    private static string? IssuerName(string? issuerDocument, string? issuerName, Dictionary<string, string> issuerNames) =>
        issuerDocument is not null && issuerNames.TryGetValue(IssuerNames.Pad(issuerDocument), out var mostUsed) ? mostUsed : issuerName;

    private sealed record GroupRow(
        long AssetId, int Assets, DateOnly? FirstMaturity, DateOnly? LastMaturity, short Block, string? ApplicationType,
        string? AssetType, string? IssuerName, string? IssuerDocument, string? Isin, string? Ticker, string? Description,
        string? MaturityGroup);

    private static AssetView ToView(Asset asset) => new(
        asset.Id, (int)asset.Block, asset.ApplicationType, asset.AssetType, asset.Isin, asset.Ticker,
        asset.Description, asset.IssuerName, asset.CountryCode, asset.MaturityDate);
}
