using System;

namespace MirraCloud.Core.ProfanityFilter.Responses
{
    /// <summary>Server reply for a <c>/profanity-filter/v1/.../check</c> call.</summary>
    [Serializable]
    public class ProfanityCheckResponse
    {
        public bool isClean;
        public string maskedText;
        public ProfanityMatchDto[] matches;
    }

    /// <summary>One matched profanity fragment inside the original text.</summary>
    [Serializable]
    public class ProfanityMatchDto
    {
        public int start;
        public int length;
        public string word;
    }
}
