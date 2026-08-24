using System;

namespace MirraCloud.Core.AssetsStorage
{
    [Serializable]
    public abstract class BaseItemStorageDto
    {
        /// <summary>
        /// Identity of this item inside the branch. Folders are addressed by it, and both
        /// <c>AssetDto.folderId</c> and <c>FolderDto.parentFolderId</c> point at a folder's id.
        /// Field names are matched verbatim against the wire, so this has to stay spelled the way
        /// the server sends it.
        /// </summary>
        public string id;

        public string name;
        public string path;

        public DateTime createdAt;
        public DateTime updatedAt;
    }
}
