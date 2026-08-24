using System;
using System.Collections.Generic;

namespace MirraCloud.Editor.Dto
{
    [Serializable]
    public class ExchangeKeyRequest
    {
        public string key;
    }

    [Serializable]
    public class ExchangeKeyResponse
    {
        public string token;
        public string expiresAtUtc;
        public string orgId;
    }

    [Serializable]
    public class EditorProjectDto
    {
        public string id;
        public string name;
        public string iconUrl;
    }

    [Serializable]
    public class EditorBranchDto
    {
        public string id;
        public string name;
        public string environment;
        public bool isActive;
        public bool isHidden;
    }

    [Serializable]
    public class EditorApiTokenDto
    {
        public string id;
        public string token;
        public string name;
        public bool isEnabled;
    }

    [Serializable]
    public class CreateApiTokenRequest
    {
        public string name;
        public string tokenType;
    }
}
