using System;

namespace Plugins.MirraCloud.Core.Services.Challenges.Dto
{
    [Serializable]
    public sealed class SubmitScoreResponseDto
    {
        public double value;
        public bool isFinished;
        public int? finishPosition;
        public bool rewardGranted;
    }
}
