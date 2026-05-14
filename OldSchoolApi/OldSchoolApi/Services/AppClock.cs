using Microsoft.Extensions.Configuration;

namespace OldSchoolApi.Services;

public static class AppClock
{
    public static TimeZoneInfo GetTimeZone(IConfiguration configuration)
    {
        var timeZoneId = configuration["App:TimeZoneId"]
            ?? configuration["TimeZone:Id"]
            ?? configuration["TimezoneId"];

        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch
            {
            }
        }

        return TimeZoneInfo.Local;
    }

    public static DateTime Now(IConfiguration configuration)
    {
        var timeZone = GetTimeZone(configuration);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
    }

    public static DateTime Today(IConfiguration configuration) => Now(configuration).Date;
}
