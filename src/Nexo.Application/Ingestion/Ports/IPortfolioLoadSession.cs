using Nexo.Application.Ingestion.Parsing;
using Nexo.Application.Ingestion.Parsing.Cda;

namespace Nexo.Application.Ingestion.Ports;

/// <summary>Recebe as linhas de um arquivo aos poucos e só grava de vez no final.</summary>
public interface IPortfolioLoadSession : IAsyncDisposable
{
    Task AddPositionAsync(PositionRecord position, CancellationToken cancellationToken);

    Task AddNetWorthAsync(NetWorthRecord netWorth, CancellationToken cancellationToken);

    Task AddRejectionAsync(string fileName, RowRejection rejection, CancellationToken cancellationToken);

    // Devolve quantas carteiras ganharam uma revisão nova.
    Task<int> CompleteAsync(CancellationToken cancellationToken);
}
