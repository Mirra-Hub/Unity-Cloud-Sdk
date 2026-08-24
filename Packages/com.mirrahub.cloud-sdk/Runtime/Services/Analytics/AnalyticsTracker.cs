using System;
using System.Collections.Generic;
using Plugins.MirraCloud.Core.Services.Analytics.Dto;
using UnityEngine;

namespace Plugins.MirraCloud.Core.Services.Analytics
{
    public class AnalyticsTracker : MonoBehaviour
    {
        private AnalyticsService _analytics;
        private float _lastReportTime;
        private float _lastBatchFlushTime;
        private float _heartbeatInterval = 300f;
        private float _batchFlushInterval = 10f;
        private int _maxBatchSize = 100;
        private bool _isTracking;

        private readonly List<BatchEventItemDto> _eventBuffer = new List<BatchEventItemDto>();
        private readonly object _bufferLock = new object();

        public static AnalyticsTracker CreateInstance()
        {
            GameObject obj = new GameObject("MirraCloudSDK Analytics Tracker");
            var tracker = obj.AddComponent<AnalyticsTracker>();
            DontDestroyOnLoad(obj);
            return tracker;
        }

        public void StartTracking(AnalyticsService analytics)
        {
            _analytics = analytics;
            _lastReportTime = Time.realtimeSinceStartup;
            _lastBatchFlushTime = Time.realtimeSinceStartup;
            _isTracking = true;
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

            float playtimeElapsed = now - _lastReportTime;
            if (playtimeElapsed >= _heartbeatInterval)
            {
                ReportPlaytime(playtimeElapsed);
                _lastReportTime = now;
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
            if (pause && _isTracking)
            {
                float elapsed = Time.realtimeSinceStartup - _lastReportTime;
                if (elapsed > 0)
                    ReportPlaytime(elapsed);
                _lastReportTime = Time.realtimeSinceStartup;

                FlushBuffer();
            }
        }

        private void OnApplicationQuit()
        {
            if (!_isTracking) return;

            float elapsed = Time.realtimeSinceStartup - _lastReportTime;
            if (elapsed > 0)
                ReportPlaytime(elapsed);

            FlushBuffer();
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
