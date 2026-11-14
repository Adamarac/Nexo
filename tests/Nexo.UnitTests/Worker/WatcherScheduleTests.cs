using Nexo.Worker;

namespace Nexo.UnitTests.Worker;

public sealed class WatcherScheduleTests
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly WatcherSchedule Schedule = new([new TimeOnly(13, 0), new TimeOnly(19, 0)], Brasilia);

    [Theory]
    [InlineData("2026-11-13T10:00:00-03:00", "2026-11-13T13:00:00-03:00")]
    [InlineData("2026-11-13T13:00:00-03:00", "2026-11-13T19:00:00-03:00")]
    [InlineData("2026-11-13T15:30:00-03:00", "2026-11-13T19:00:00-03:00")]
    [InlineData("2026-11-13T20:00:00-03:00", "2026-11-14T13:00:00-03:00")]
    public void NextRunAfter_PicksTheNextTimeInBrasilia(string now, string expected)
    {
        Schedule.NextRunAfter(DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture))
            .Should().Be(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void NextRunAfter_WorksWhenTheClockIsInUtc()
    {
        var nowInUtc = new DateTimeOffset(2026, 11, 13, 17, 0, 0, TimeSpan.Zero);

        Schedule.NextRunAfter(nowInUtc).Should().Be(new DateTimeOffset(2026, 11, 13, 19, 0, 0, TimeSpan.FromHours(-3)));
    }
}
