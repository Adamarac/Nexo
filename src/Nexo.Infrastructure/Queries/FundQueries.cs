using Microsoft.EntityFrameworkCore;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Funds;
using Nexo.Domain.Common;
using Nexo.Domain.Funds;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Queries;

internal sealed class FundQueries(NexoDbContext db) : IFundQueries
{
    public async Task<PagedResult<FundSummary>> SearchAsync(FundSearch search, CancellationToken cancellationToken)
    {
        var funds = Filter(db.Funds.AsNoTracking(), search);
        var total = await funds.CountAsync(cancellationToken);

        var page = await Sort(funds, search.Sort)
            .Skip(search.Skip)
            .Take(search.PageSize)
            .Select(fund => new
            {
                fund.Id,
                fund.Cnpj,
                fund.Name,
                fund.FundType,
                fund.Situation,
                fund.NetWorth,
                LatestReferenceDate = db.Portfolios
                    .Where(portfolio => portfolio.FundId == fund.Id && portfolio.IsCurrent)
                    .Max(portfolio => (DateOnly?)portfolio.ReferenceDate),
            })
            .ToListAsync(cancellationToken);

        var items = page
            .Select(fund => new FundSummary(fund.Id, fund.Cnpj.Value, fund.Name, fund.FundType, fund.Situation, fund.NetWorth, fund.LatestReferenceDate))
            .ToList();

        return new PagedResult<FundSummary>(items, search.Page, search.PageSize, total);
    }

    public async Task<FundDetails?> GetAsync(long fundId, CancellationToken cancellationToken)
    {
        var fund = await db.Funds.AsNoTracking().Include(fund => fund.Classes).SingleOrDefaultAsync(fund => fund.Id == fundId, cancellationToken);

        if (fund is null)
            return null;

        var referenceDates = await db.Portfolios
            .Where(portfolio => portfolio.FundId == fundId && portfolio.IsCurrent)
            .Select(portfolio => portfolio.ReferenceDate)
            .Distinct()
            .OrderByDescending(date => date)
            .ToListAsync(cancellationToken);

        var classes = fund.Classes
            .OrderBy(fundClass => fundClass.Name)
            .Select(fundClass => new FundClassView(
                fundClass.Id, fundClass.Cnpj.Value, fundClass.Name, fundClass.ClassType, fundClass.Situation,
                fundClass.AnbimaClassification, fundClass.TargetAudience))
            .ToList();

        return new FundDetails(
            fund.Id, fund.Cnpj.Value, fund.Name, fund.FundType, fund.Situation, fund.AdministratorName, fund.ManagerName,
            fund.NetWorth, fund.NetWorthDate, fund.IsPlaceholder, classes, referenceDates);
    }

    // Um CNPJ válido procura o fundo e as classes dele; qualquer outro texto procura no nome.
    private static IQueryable<Fund> Filter(IQueryable<Fund> funds, FundSearch search)
    {
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            funds = Cnpj.TryParse(search.Search, out var cnpj)
                ? funds.Where(fund => fund.Cnpj == cnpj || fund.Classes.Any(fundClass => fundClass.Cnpj == cnpj))
                : funds.Where(fund => EF.Functions.ILike(fund.Name, LikePattern.Contains(search.Search)));
        }

        if (!string.IsNullOrWhiteSpace(search.Type))
            funds = funds.Where(fund => fund.FundType == search.Type);

        if (!string.IsNullOrWhiteSpace(search.Situation))
            funds = funds.Where(fund => fund.Situation == search.Situation);

        return funds;
    }

    private static IQueryable<Fund> Sort(IQueryable<Fund> funds, string? sort) => sort switch
    {
        "-name" => funds.OrderByDescending(fund => fund.Name).ThenBy(fund => fund.Id),
        "netWorth" => funds.OrderBy(fund => fund.NetWorth == null).ThenBy(fund => fund.NetWorth).ThenBy(fund => fund.Id),
        "-netWorth" => funds.OrderBy(fund => fund.NetWorth == null).ThenByDescending(fund => fund.NetWorth).ThenBy(fund => fund.Id),
        _ => funds.OrderBy(fund => fund.Name).ThenBy(fund => fund.Id),
    };
}
