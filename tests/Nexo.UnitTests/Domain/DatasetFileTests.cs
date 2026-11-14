using Nexo.Domain.Ingestion;

namespace Nexo.UnitTests.Domain;

public sealed class DatasetFileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(15);

    private static DatasetFile NewFile() => DatasetFile.Downloaded(
        new DatasetSource("cda_fi_202609", DatasetKind.CdaMonthly, "https://exemplo/cda_fi_202609.zip"),
        new RemoteVersion("\"abc\"", Now, 100),
        new string('a', 64),
        "cda_fi_202609/a.zip",
        Now);

    [Fact]
    public void StartProcessing_MovesDownloadedFileToProcessing()
    {
        var file = NewFile();

        file.StartProcessing(Now, Lease).Should().Be(ProcessingStart.Started);

        file.Status.Should().Be(DatasetFileStatus.Processing);
        file.Attempts.Should().Be(1);
        file.LeaseUntil.Should().Be(Now + Lease);
    }

    [Fact]
    public void StartProcessing_IsRefusedWhileAnotherWorkerHoldsTheLease()
    {
        var file = NewFile();
        file.StartProcessing(Now, Lease);

        file.StartProcessing(Now.AddMinutes(1), Lease).Should().Be(ProcessingStart.Busy);
    }

    [Fact]
    public void StartProcessing_TakesOverAnExpiredLease()
    {
        var file = NewFile();
        file.StartProcessing(Now, Lease);

        file.StartProcessing(Now + Lease + TimeSpan.FromSeconds(1), Lease).Should().Be(ProcessingStart.Started);
        file.Attempts.Should().Be(2);
    }

    [Fact]
    public void StartProcessing_ReportsAlreadyProcessedFiles()
    {
        var file = NewFile();
        file.StartProcessing(Now, Lease);
        file.Complete(new IngestionCounts(10, 8, 2, 3), Now);

        file.StartProcessing(Now, Lease).Should().Be(ProcessingStart.AlreadyProcessed);
    }

    [Fact]
    public void Complete_RejectsCountsThatDoNotAddUp()
    {
        var file = NewFile();
        file.StartProcessing(Now, Lease);

        var act = () => file.Complete(new IngestionCounts(10, 8, 1, 3), Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_StoresCountsAndReleasesLease()
    {
        var file = NewFile();
        file.StartProcessing(Now, Lease);

        file.Complete(new IngestionCounts(10, 8, 2, 3), Now);

        file.Status.Should().Be(DatasetFileStatus.Processed);
        file.RowsRead.Should().Be(10);
        file.RowsRejected.Should().Be(2);
        file.PortfoliosChanged.Should().Be(3);
        file.LeaseUntil.Should().BeNull();
    }

    [Fact]
    public void Fail_KeepsTheErrorAndAllowsAnotherAttempt()
    {
        var file = NewFile();
        file.StartProcessing(Now, Lease);

        file.Fail("arquivo corrompido");

        file.Status.Should().Be(DatasetFileStatus.Failed);
        file.LastError.Should().Be("arquivo corrompido");
        file.StartProcessing(Now, Lease).Should().Be(ProcessingStart.Started);
    }

    [Fact]
    public void ReleaseLease_LetsAnotherWorkerStartRightAway()
    {
        var file = NewFile();
        file.StartProcessing(Now, Lease);

        file.ReleaseLease();

        file.Status.Should().Be(DatasetFileStatus.Downloaded);
        file.StartProcessing(Now.AddSeconds(1), Lease).Should().Be(ProcessingStart.Started);
    }

    [Fact]
    public void RequestReprocess_IsNotAllowedWhileProcessing()
    {
        var file = NewFile();
        file.StartProcessing(Now, Lease);

        file.RequestReprocess(Now).Should().BeFalse();
    }

    [Fact]
    public void RequestReprocess_ReturnsProcessedFileToTheQueue()
    {
        var file = NewFile();
        file.StartProcessing(Now, Lease);
        file.Complete(new IngestionCounts(1, 1, 0, 1), Now);

        file.RequestReprocess(Now).Should().BeTrue();
        file.Status.Should().Be(DatasetFileStatus.Downloaded);
    }

    [Fact]
    public void Supersede_AlsoReachesAFileWaitingToBeReprocessed()
    {
        var file = NewFile();
        file.StartProcessing(Now, Lease);
        file.Complete(new IngestionCounts(1, 1, 0, 1), Now);
        file.RequestReprocess(Now);

        file.Supersede();

        // Na fila ou não, a versão antiga não pode voltar a ser processada depois da nova.
        file.Status.Should().Be(DatasetFileStatus.Superseded);
        file.StartProcessing(Now, Lease).Should().Be(ProcessingStart.AlreadyProcessed);
    }

    [Fact]
    public void Supersede_LeavesAFileBeingProcessedAlone()
    {
        var file = NewFile();
        file.StartProcessing(Now, Lease);

        file.Supersede();

        file.Status.Should().Be(DatasetFileStatus.Processing);
    }

    [Fact]
    public void MatchesRemote_ComparesEtagAndSize()
    {
        var file = NewFile();

        file.MatchesRemote(new RemoteVersion("\"abc\"", null, 100)).Should().BeTrue();
        file.MatchesRemote(new RemoteVersion("\"xyz\"", null, 100)).Should().BeFalse();
        file.MatchesRemote(new RemoteVersion("\"abc\"", null, 101)).Should().BeFalse();
    }
}
