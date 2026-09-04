using System;

namespace MirraCloud.Core.AssetsStorage
{
    [Serializable]
    public class FolderDto : BaseItemStorageDto
    {
        public string parentFolderId;

        /// <summary>Whether this folder was published on its own.</summary>
        public bool isPublic;

        /// <summary>Whether a folder above this one publishes it.</summary>
        public bool isPublicInherited;
    }
}