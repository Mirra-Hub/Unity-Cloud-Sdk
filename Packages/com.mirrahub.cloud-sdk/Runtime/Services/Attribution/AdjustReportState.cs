namespace MirraCloud.Core.Attribution
{
    /// <summary>Where the queued Adjust report (<see cref="AttributionService.ReportAdjustAdid"/>) stands.</summary>
    public enum AdjustReportState
    {
        /// <summary>The game has reported nothing from Adjust in this run.</summary>
        Idle = 0,

        /// <summary>Attribution is in, the adid is not — Adjust has not handed it out yet. Sent once it arrives.</summary>
        WaitingForAdid = 1,

        /// <summary>No player session yet. Sent right after the sign-in or the session restore.</summary>
        WaitingForSession = 2,

        /// <summary>The report is on its way.</summary>
        Sending = 3,

        /// <summary>The server has what the game reported, for the account that is signed in.</summary>
        Sent = 4,

        /// <summary>
        /// The last attempt did not get through (no connection, 5xx, the session ended, the request could not start).
        /// Sent again on the next sign-in, session refresh or report — the SDK does not retry on a timer.
        /// </summary>
        Failed = 5,

        /// <summary>
        /// The server refused the report for good: see <see cref="AttributionService.LastAdjustReport"/>. Not resent
        /// until the game reports something new or another sign-in happens; when the project has no enabled Adjust
        /// integration, not resent at all until the next launch.
        /// </summary>
        Rejected = 6,
    }
}
