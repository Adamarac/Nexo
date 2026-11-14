using Nexo.Infrastructure.Cvm;

namespace Nexo.UnitTests.Ingestion;

public sealed class CvmDirectoryListingTests
{
    // Trecho real da página de listagem da CVM (nginx autoindex).
    private const string Html = """
        <a href="/.theme/style.css">x</a><a href="../">../</a>
        <a href="HIST/">HIST/</a>
        <a href="cda_fi_202609.zip">cda_fi_202609.zip</a>
        <a href="cda_fi_202610.zip">cda_fi_202610.zip</a>
        <a href="https://github.com/manala/nginx-autoindex-theme">tema</a>
        """;

    [Fact]
    public void FileNames_KeepsOnlyFilesOfTheFolder()
    {
        CvmDirectoryListing.FileNames(Html).Should().Equal("cda_fi_202609.zip", "cda_fi_202610.zip");
    }
}
