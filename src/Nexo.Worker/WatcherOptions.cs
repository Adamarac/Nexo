using System.Globalization;

namespace Nexo.Worker;

public sealed class WatcherOptions
{
    public const string Section = "Watcher";

    public bool Enabled { get; set; } = true;

    public bool RunOnStartup { get; set; } = true;

    // A CVM publica às 8h; o servidor dela pode levar até 4h para mostrar a versão nova.
    public string[] RunAt { get; set; } = [];

    public string TimeZone { get; set; } = "America/Sao_Paulo";

    public WatcherSchedule ToSchedule()
    {
        var times = (RunAt.Length > 0 ? RunAt : ["13:00", "19:00"])
            .Select(time => TimeOnly.Parse(time, CultureInfo.InvariantCulture))
            .ToList();

        return new WatcherSchedule(times, TimeZoneInfo.FindSystemTimeZoneById(TimeZone));
    }
}
