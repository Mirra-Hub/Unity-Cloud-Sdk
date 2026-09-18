using System;
using MirraCloud.Core.Events.Enums;

namespace MirraCloud.Core.Events.Dto
{
    [Serializable]
    public sealed class ActiveEventDto
    {
        public string key;
        public string name;
        public string description;
        public int priority;
        public EventScheduleType scheduleType;
        public bool isMatched;
        public bool isTargeted;
        public DateTime startsAtUtc;
        public DateTime endsAtUtc;
        public DateTime? nextOccurrenceAtUtc;
        public DateTime? scheduleEndsAtUtc;
    }
}
