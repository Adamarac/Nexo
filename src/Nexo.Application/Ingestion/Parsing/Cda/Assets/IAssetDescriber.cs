namespace Nexo.Application.Ingestion.Parsing.Cda.Assets;

/// <summary>Sabe montar o ativo a partir das colunas específicas de um bloco do CDA.</summary>
internal interface IAssetDescriber
{
    // Colunas do bloco que não viram campo próprio, mas ficam guardadas como detalhe da posição.
    IReadOnlyList<string> DetailColumns { get; }

    AssetRecord Describe(RowReader row, string applicationType, string? assetType);
}
