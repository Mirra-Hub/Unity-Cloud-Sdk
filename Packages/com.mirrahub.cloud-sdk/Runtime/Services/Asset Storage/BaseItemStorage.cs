using System;

namespace MirraCloud.Core.AssetsStorage
{
    public abstract class BaseItemStorage
    {
        /// <summary>Identity inside the branch. Folders are addressed by it, and it is what
        /// <c>Asset.FolderId</c> and <c>Folder.ParentFolderId</c> point at.</summary>
        public readonly string ItemId;

        public readonly string Name;
        public readonly string Path;
        public readonly DateTime CreatedAt;
        public readonly DateTime UpdatedAt;

        protected BaseItemStorage(BaseItemStorageDto dto)
        {
            ItemId = dto.id;
            Name = dto.name;
            CreatedAt = dto.createdAt;
            UpdatedAt = dto.updatedAt;
            Path = dto.path;
        }
    }
}
