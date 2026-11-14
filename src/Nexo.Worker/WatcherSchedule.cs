namespace Nexo.Worker;

/// <summary>Calcula o próximo horário de conferir a CVM, no fuso de Brasília.</summary>
public sealed class WatcherSchedule(IReadOnlyList<TimeOnly> times, TimeZoneInfo timeZone)
{
    public DateTimeOffset NextRunAfter(DateTimeOffset now)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);

        // Olhamos os horários de hoje e de amanhã e pegamos o primeiro que ainda não passou.
        var next = new[] { today, today.AddDays(1) }
            .SelectMany(day => times.Select(time => day.ToDateTime(time)))
            .Where(candidate => candidate > localNow.DateTime)
            .Min();

        return new DateTimeOffset(next, timeZone.GetUtcOffset(next));
    }
}
