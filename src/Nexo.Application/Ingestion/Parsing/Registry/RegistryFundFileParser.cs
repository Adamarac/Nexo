namespace Nexo.Application.Ingestion.Parsing.Registry;

internal sealed class RegistryFundFileParser : IRegistryFileParser
{
    public IReadOnlyCollection<string> RequiredColumns { get; } =
        ["ID_Registro_Fundo", "CNPJ_Fundo", "Denominacao_Social", "Data_Registro"];

    public ParsedRegistryRow Parse(CsvRow row)
    {
        var reader = new RowReader(row);
        var registryId = reader.RequiredLong("ID_Registro_Fundo");
        var cnpj = reader.RequiredCnpj("CNPJ_Fundo");
        var name = reader.RequiredText("Denominacao_Social");
        var registeredAt = reader.Date("Data_Registro");
        var netWorth = reader.Decimal("Patrimonio_Liquido");
        var netWorthDate = reader.Date("Data_Patrimonio_Liquido");

        if (reader.Rejection is { } rejection)
            return ParsedRegistryRow.Rejected(rejection);

        return ParsedRegistryRow.Of(new RegistryFundRecord(registryId, cnpj, name, registeredAt)
        {
            FundType = reader.Text("Tipo_Fundo"),
            Situation = reader.Text("Situacao"),
            AdministratorName = reader.Text("Administrador"),
            ManagerName = reader.Text("Gestor"),
            NetWorth = netWorth,
            NetWorthDate = netWorthDate,
        });
    }
}
