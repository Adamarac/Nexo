using Nexo.Domain.Ingestion;

namespace Nexo.Application.Ingestion.Ports;

/// <summary>Acesso aos arquivos publicados no portal de dados abertos da CVM.</summary>
public interface ICvmSource
{
    // O cadastro vem antes das carteiras, para os fundos já existirem quando as posições chegarem.
    Task<IReadOnlyList<DatasetSource>> ListDatasetsAsync(CancellationToken cancellationToken);

    Task<RemoteVersion> GetRemoteVersionAsync(DatasetSource source, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(DatasetSource source, CancellationToken cancellationToken);
}
