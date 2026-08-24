using System;

namespace Plugins.MirraCloud.Core.Services.Challenges.Dto
{
    [Serializable]
    public sealed class ChallengeEntriesDto
    {
        public string challengeId;
        public ChallengeEntryDto[] entries;
    }
}
