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

        /// <summary>Whether the anonymous <c>LoadPublic*</c> routes will serve it.</summary>
        public readonly bool IsPublic;

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
        }
    }
}