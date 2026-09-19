using System;

namespace MirraCloud.Core.Events.Dto
{
    [Serializable]
    public sealed class ActiveEventsDto
    {
        public DateTime serverTimeUtc;
        public long generation;
        public DateTime? staleAfterUtc;
        public ActiveEventDto[] events;
    }
}
