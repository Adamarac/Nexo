namespace Nexo.Application.Queries;

/// <summary>Guarda respostas de consultas caras até chegarem dados novos.</summary>
public interface IQueryCache
{
    Task<T> GetOrCreateAsync<T>(string area, object parameters, Func<CancellationToken, Task<T>> create, CancellationToken cancellationToken);
}
