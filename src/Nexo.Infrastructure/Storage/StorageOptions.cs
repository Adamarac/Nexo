namespace Nexo.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    public string Root { get; set; } = "data/files";
}
