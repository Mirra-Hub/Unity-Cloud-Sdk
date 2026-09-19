namespace MirraCloud.Core.Events.Enums
{
    /// <summary>How an event repeats. Numbers match the backend enum; it sends the name as a string.</summary>
    public enum EventScheduleType
    {
        /// <summary>Runs once, between its start and end.</summary>
        OneShot = 0,

        /// <summary>Opens again on a weekly pattern — a happy hour, a weekend bonus.</summary>
        Recurring = 1
    }
}
