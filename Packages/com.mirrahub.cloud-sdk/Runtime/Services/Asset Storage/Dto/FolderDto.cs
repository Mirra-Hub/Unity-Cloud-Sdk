using System;

namespace MirraCloud.Core.AssetsStorage
{
    [Serializable]
    public class FolderDto : BaseItemStorageDto
    {
        public string parentFolderId;
    }
}