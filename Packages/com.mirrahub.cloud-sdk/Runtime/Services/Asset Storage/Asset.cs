namespace MirraCloud.Core.AssetsStorage
{
    public class Asset : BaseItemStorage
    {
        /// <summary>Survives a re-import and is the same in every environment — the id every
        /// <c>Load*FromId</c> call takes, and the one worth persisting.</summary>
        public readonly string StableId;

        public readonly string FolderId;
        public readonly string MimeType;
        public readonly long Size;
        public readonly int Version;
        public readonly AssetType Type;
        public readonly string Extension;

        /// <summary>Whether this asset was published on its own.</summary>
        public readonly bool IsPublic;

        /// <summary>Whether the folder this asset sits in publishes it.</summary>
        public readonly bool IsPublicInherited;

        /// <summary>Whether the anonymous <c>LoadPublic*</c> routes will serve it — either flag is
        /// enough. Publishing a folder publishes everything inside it, so an asset can be servable
        /// anonymously without <see cref="IsPublic"/> ever having been set on it.</summary>
        public bool IsEffectivelyPublic => IsPublic || IsPublicInherited;

        public Asset(AssetDto dto) : base(dto)
        {
            StableId = dto.stableId;
            FolderId = dto.folderId;
            MimeType = dto.mimeType;
            Size = dto.size; 
            Version = dto.version;
            Type = dto.type; 
            Extension = dto.extension;
            IsPublic = dto.isPublic;
            IsPublicInherited = dto.isPublicInherited;
        }
    }
}