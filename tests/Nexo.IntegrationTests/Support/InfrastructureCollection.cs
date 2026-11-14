namespace Nexo.IntegrationTests.Support;

[CollectionDefinition(Name)]
public sealed class InfrastructureCollection : ICollectionFixture<InfrastructureFixture>
{
    public const string Name = "infraestrutura";
}
