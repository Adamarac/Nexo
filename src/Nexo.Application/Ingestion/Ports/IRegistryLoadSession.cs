using Nexo.Application.Ingestion.Parsing;
using Nexo.Application.Ingestion.Parsing.Registry;

namespace Nexo.Application.Ingestion.Ports;

public interface IRegistryLoadSession : IAsyncDisposable
{
    Task AddFundAsync(RegistryFundRecord fund, CancellationToken cancellationToken);

    Task AddClassAsync(RegistryClassRecord fundClass, CancellationToken cancellationToken);

    Task AddRejectionAsync(string fileName, RowRejection rejection, CancellationToken cancellationToken);

    // Devolve quantos fundos e classes foram criados ou atualizados.
    Task<int> CompleteAsync(CancellationToken cancellationToken);
}
