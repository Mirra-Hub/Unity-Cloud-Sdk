namespace MirraCloud.Core.AssetsStorage
{
    public class Folder : BaseItemStorage
    {
        public readonly string ParentFolderId;

        /// <summary>Whether this folder was published on its own.</summary>
        public readonly bool IsPublic;

        /// <summary>Whether a folder above this one publishes it.</summary>
        public readonly bool IsPublicInherited;

        /// <summary>Whether this folder publishes what it contains, from either flag.</summary>
        public bool IsEffectivelyPublic => IsPublic || IsPublicInherited;

        public Folder(FolderDto dto) : base(dto)
        {
            ParentFolderId = dto.parentFolderId;
            IsPublic = dto.isPublic;
            IsPublicInherited = dto.isPublicInherited;
        }
    }
}