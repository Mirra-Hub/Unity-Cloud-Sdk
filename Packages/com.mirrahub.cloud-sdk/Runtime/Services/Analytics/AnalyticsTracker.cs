using System;
using System.Collections.Generic;
using Plugins.MirraCloud.Core.Services.Analytics.Dto;
using UnityEngine;

namespace Plugins.MirraCloud.Core.Services.Analytics
{
    /// <summary>
    /// Runs analytics for as long as a player session does: the play session, the playtime heartbeat and
    /// the batched event buffer. The SDK starts it on sign-in or when a saved session is restored, and
    /// stops it on sign-out or session expiry.
    /// <para>
    /// A play session is one entry into the game: it starts with the launch (or a sign-in after signing
    /// out, or as another account) and lasts until the app is closed or the player signs out. Coming back
    /// from the background continues it. Time in the background is not playtime, though: a pause, and on
    /// WebGL and desktop — where leaving does not pause the app — a loss of focus as well. On mobile focus
    /// does not count, since the keyboard and system overlays take it while the player is still in the game.
    /// </para>
    /// </summary>
    public class AnalyticsTracker : MonoBehaviour
    {
        private AnalyticsService _analytics;
        private float _lastReportTime;
        private float _lastBatchFlushTime;
        private float _heartbeatInterval = 300f;
        private float _batchFlushInterval = 10f;
        private int _maxBatchSize = 100;
        private bool _isTracking;

        private string _accountId;

        private bool _isAway;
        private float _awaySinceRealtime;

        private readonly List<BatchEventItemDto> _eventBuffer = new List<BatchEventItemDto>();
        private readonly object _bufferLock = new object();

        public static AnalyticsTracker CreateInstance()
        {
            GameObject obj = new GameObject("MirraCloudSDK Analytics Tracker");
            var tracker = obj.AddComponent<AnalyticsTracker>();
            DontDestroyOnLoad(obj);
            return tracker;
        }

        /// <summary>
        /// Starts tracking with a new play session — reporting <c>SessionsStarted</c> for it — and restarts
        /// the playtime and batch clocks. Tracking that is already running is stopped first. The SDK calls
        /// this itself; there is normally no reason to.
        /// </summary>
        public void StartTracking(AnalyticsService analytics)
        {
            Restart(analytics, null);
        }

        /// <summary>
        /// Stops the heartbeat and the batch flushes and ends the play session. Events still buffered are
        /// dropped: by the time the SDK stops tracking the token they were recorded under is already gone
        /// (sign-out) or already belongs to the next account (switch), so they could not be delivered as the
        /// player who produced them. Safe to call when not tracking.
        /// </summary>
        public void StopTracking()
        {
            if (!_isTracking)
            {
                return;
            }

            _isTracking = false;
            _accountId = null;

            int dropped;
            lock (_bufferLock)
            {
                dropped = _eventBuffer.Count;
                _eventBuffer.Clear();
            }

            if (dropped > 0)
            {
                Debug.LogWarning($"Analytics stopped with {dropped} buffered event(s) unsent; they were dropped along with the player session.");
            }

            _analytics?.EndSession();
        }

        /// <summary>
        /// Sign-in (<c>OnLogin</c>). Starts a play session unless one is already running for this account:
        /// linking a provider raises <c>OnLogin</c> for the account that is already signed in, and must not
        /// cut the session short or reset the heartbeat.
        /// </summary>
        internal void TrackSignIn(AnalyticsService analytics, string accountId)
        {
            if (_isTracking && (accountId == null || _accountId == null || accountId == _accountId))
            {
                _accountId ??= accountId;
                return;
            }

            Restart(analytics, accountId);
        }

        /// <summary>
        /// Session refresh (<c>OnSessionRefreshed</c>). The restore of a saved session at launch is the
        /// only sign-in a returning player gets, so it starts tracking; the refresh that follows a 401
        /// mid-play changes nothing.
        /// </summary>
        internal void TrackSessionRefresh(AnalyticsService analytics, string accountId)
        {
            if (_isTracking)
            {
                _accountId ??= accountId;
                return;
            }

            Restart(analytics, accountId);
        }

        private void Restart(AnalyticsService analytics, string accountId)
        {
            if (analytics == null)
            {
                return;
            }

            StopTracking();

            _analytics = analytics;
            _accountId = accountId;
            _isTracking = true;
            BeginPlaySession();
        }

        private void BeginPlaySession()
        {
            float now = Time.realtimeSinceStartup;
            _lastReportTime = now;
            _lastBatchFlushTime = now;

            if (_isAway)
            {
                _awaySinceRealtime = now;
            }

            _analytics.StartSession();
        }

        public void EnqueueEvent(string eventName, Dictionary<string, string> parameters = null, List<string> tags = null)
        {
            if (!_isTracking) return;

            var item = new BatchEventItemDto
            {
                EventName = eventName,
                Date = DateTime.UtcNow.ToString("O")
            };

            if (parameters != null)
                item.Parameters = parameters;

            if (tags != null)
                item.Tags = tags;

            lock (_bufferLock)
            {
                _eventBuffer.Add(item);

                if (_eventBuffer.Count >= _maxBatchSize)
                    FlushBuffer();
            }
        }

        private void Update()
        {
            if (!_isTracking) return;

            float now = Time.realtimeSinceStartup;

            if (!_isAway)
            {
                float playtimeElapsed = now - _lastReportTime;
                if (playtimeElapsed >= _heartbeatInterval)
                {
                    ReportPlaytime(playtimeElapsed);
                    _lastReportTime = now;
                }
            }

            float batchElapsed = now - _lastBatchFlushTime;
            if (batchElapsed >= _batchFlushInterval)
            {
                FlushBuffer();
                _lastBatchFlushTime = now;
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                LeaveForeground();

                if (_isTracking)
                {
                    float elapsed = UnreportedPlaytime();
                    if (elapsed > 0)
                        ReportPlaytime(elapsed);
                    _lastReportTime = Time.realtimeSinceStartup;

                    FlushBuffer();
                }
            }
            else
            {
                ReturnToForeground();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!FocusLossMeansAway())
            {
                return;
            }

            if (hasFocus)
            {
                ReturnToForeground();
            }
            else
            {
                LeaveForeground();
            }
        }

        /// <summary>
        /// Whether losing focus means the player has left: only where leaving does not pause the app —
        /// WebGL (another tab) and desktop (another window), the Editor included. Everywhere else the pause
        /// is the signal. On mobile focus goes to the on-screen keyboard, the notification shade, permission
        /// and purchase dialogs and split-screen while the player is still in the game, and that time is
        /// playtime.
        /// </summary>
        private static bool FocusLossMeansAway()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WebGLPlayer:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                    return true;
                default:
                    return false;
            }
        }

        private void OnApplicationQuit()
        {
            if (!_isTracking) return;

            float elapsed = UnreportedPlaytime();
            if (elapsed > 0)
                ReportPlaytime(elapsed);

            FlushBuffer();
        }

        private void LeaveForeground()
        {
            if (_isAway)
            {
                return;
            }

            _isAway = true;
            _awaySinceRealtime = Time.realtimeSinceStartup;
        }

        private void ReturnToForeground()
        {
            if (!_isAway)
            {
                return;
            }

            float playedBeforeLeaving = Mathf.Max(0f, UnreportedPlaytime());
            _isAway = false;

            _lastReportTime = Time.realtimeSinceStartup - playedBeforeLeaving;
        }

        /// <summary>Playtime since the last report, not counting any time away that is still going on.</summary>
        private float UnreportedPlaytime()
        {
            float until = _isAway ? _awaySinceRealtime : Time.realtimeSinceStartup;
            return until - _lastReportTime;
        }

        private void FlushBuffer()
        {
            List<BatchEventItemDto> toSend;

            lock (_bufferLock)
            {
                if (_eventBuffer.Count == 0) return;

                toSend = new List<BatchEventItemDto>(_eventBuffer);
                _eventBuffer.Clear();
            }

            _analytics.SendBatchAsync(toSend);
        }

        private void ReportPlaytime(float seconds)
        {
            int minutes = Mathf.Max(1, Mathf.RoundToInt(seconds / 60f));
            _analytics.SendPlaytimeAsync(minutes);
        }
    }
}
