using System.Collections.Generic;

namespace MirraCloud.Core.CloudSave.Requests
{
    public class DeleteKeysRequest
    {
        public List<string> keys = new List<string>();

        public DeleteKeysRequest() { }

        public DeleteKeysRequest(params string[] keys)
        {
            this.keys = new List<string>(keys);
        }
    }
}
