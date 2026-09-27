using System;

namespace MirraCloud.Core.AssetsStorage
{
    public abstract class BaseItemStorage
    {
        /// <summary>Identity inside the branch. Folders are addressed by it, and it is what
        /// <c>Asset.FolderId</c> and <c>Folder.ParentFolderId</c> point at.</summary>
        public readonly string ItemId;

        public readonly string Name;

        /// <summary>Where the item sits in the branch: <c>/icons/coin.png</c> for an asset, <c>/icons</c>
        /// for a folder. What <c>Load*FromPath</c> and <c>GetAssetsInFolder</c> take, with or without the
        /// leading slash; case-sensitive.</summary>
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
