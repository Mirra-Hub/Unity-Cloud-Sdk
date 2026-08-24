using System;

namespace Plugins.MirraCloud.Core.Services.Challenges.Dto
{
    [Serializable]
    public sealed class ChallengeEntryDto
    {
        public string playerId;
        public string playerName;
        public int position;
        public double value;
        public bool isFinished;
        public int? finishPosition;
        public DateTime? finishedAt;
    }
}
