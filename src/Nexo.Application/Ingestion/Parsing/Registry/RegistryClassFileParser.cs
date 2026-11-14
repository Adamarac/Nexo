namespace Nexo.Application.Ingestion.Parsing.Registry;

internal sealed class RegistryClassFileParser : IRegistryFileParser
{
    public IReadOnlyCollection<string> RequiredColumns { get; } =
        ["ID_Registro_Fundo", "CNPJ_Classe", "Denominacao_Social", "Data_Registro"];

    public ParsedRegistryRow Parse(CsvRow row)
    {
        var reader = new RowReader(row);
        var fundRegistryId = reader.RequiredLong("ID_Registro_Fundo");
        var cnpj = reader.RequiredCnpj("CNPJ_Classe");
        var name = reader.RequiredText("Denominacao_Social");
        var registeredAt = reader.Date("Data_Registro");

        if (reader.Rejection is { } rejection)
            return ParsedRegistryRow.Rejected(rejection);

        return ParsedRegistryRow.Of(new RegistryClassRecord(fundRegistryId, cnpj, name, registeredAt)
        {
            ClassType = reader.Text("Tipo_Classe"),
            Situation = reader.Text("Situacao"),
            AnbimaClassification = reader.Text("Classificacao_Anbima"),
            TargetAudience = reader.Text("Publico_Alvo"),
        });
    }
}
