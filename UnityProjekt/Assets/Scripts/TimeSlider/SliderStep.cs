using System;

namespace TimeSlider
{
    public class SliderStep
    {
        public int Min { get; }
        public int Max { get; }
        public Types Type { get; }

        private const string DateFormat = "HH:mm:ss dd.MM.yyyy";

        public SliderStep(int min, int max, Types type)
        {
            Min = min;
            Max = max;
            Type = type;
        }

        public string Name => Type.ToString();

        public string ToMinDateString(DateTime currentTime) => FormatDate(ToMinDate(currentTime));
        public string ToMaxDateString(DateTime currentTime) => FormatDate(ToMaxDate(currentTime));

        public DateTime ToMinDate(DateTime currentTime) => ShiftDate(currentTime, -(Max / 2));
        public DateTime ToMaxDate(DateTime currentTime) => ShiftDate(currentTime, Max / 2);

        public float ToSliderValue(DateTime currentTime) => Type switch
        {
            Types.Month  => currentTime.Month,
            Types.Day    => currentTime.Day,
            Types.Hour   => currentTime.Hour,
            Types.Minute => currentTime.Minute,
            Types.Second => currentTime.Second,
            _ => throw new ArgumentOutOfRangeException(nameof(Type), $"Unsupported slider type: {Type}")
        };

        public DateTime SetDate(DateTime currentTime, int value) => Type switch
        {
            Types.Month  => new DateTime(currentTime.Year, value, currentTime.Day, currentTime.Hour, currentTime.Minute, currentTime.Second, DateTimeKind.Local),
            Types.Day    => new DateTime(currentTime.Year, currentTime.Month, value, currentTime.Hour, currentTime.Minute, currentTime.Second, DateTimeKind.Local),
            Types.Hour   => new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, value, currentTime.Minute, currentTime.Second, DateTimeKind.Local),
            Types.Minute => new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, currentTime.Hour, value, currentTime.Second, DateTimeKind.Local),
            Types.Second => new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, currentTime.Hour, currentTime.Minute, value, DateTimeKind.Local),
            _ => throw new ArgumentOutOfRangeException(nameof(Type), $"Unsupported date set type: {Type}")
        };

        private DateTime ShiftDate(DateTime currentTime, int amount) => Type switch
        {
            Types.Month  => currentTime.AddMonths(amount),
            Types.Day    => currentTime.AddDays(amount),
            Types.Hour   => currentTime.AddHours(amount),
            Types.Minute => currentTime.AddMinutes(amount),
            Types.Second => currentTime.AddSeconds(amount),
            _ => throw new ArgumentOutOfRangeException(nameof(Type), $"Unsupported date shift type: {Type}")
        };

        private static string FormatDate(DateTime dateTime) => dateTime.ToString(DateFormat);

        public enum Types
        {
            Month,
            Day,
            Hour,
            Minute,
            Second
        }
    }
}
