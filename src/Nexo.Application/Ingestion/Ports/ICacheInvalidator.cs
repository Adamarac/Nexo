namespace Nexo.Application.Ingestion.Ports;

/// <summary>Avisa a API que há dados novos, para ela parar de usar respostas antigas do cache.</summary>
public interface ICacheInvalidator
{
    Task AdvanceEpochAsync(CancellationToken cancellationToken);
}
