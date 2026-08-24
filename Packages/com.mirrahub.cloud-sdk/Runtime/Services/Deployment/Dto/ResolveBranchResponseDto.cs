using System;

namespace Plugins.MirraCloud.Core.Services.Deployment.Dto
{
    [Serializable]
    public class ResolveBranchResponseDto
    {
        public string branchId;
        public string branchName;
        public string buildVersion;
    }
}