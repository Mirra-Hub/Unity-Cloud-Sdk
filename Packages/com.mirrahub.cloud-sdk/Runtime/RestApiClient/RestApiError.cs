using System;
using System.Collections.Generic;
using MirraCloud.Core.Errors;
using UnityEngine.Networking;

namespace MirraCloud.Core
{
    [Serializable]
    public sealed class RestApiError
    {
        public RestApiErrorType Type;
        public string Message;

        public string Method;
        public string Route;
        public string Url;

        public long? HttpStatusCode;
        public UnityWebRequest.Result? NetworkResult;

        public string ResponseBody;

        /// <summary>
        /// Parsed typed errors from the backend envelope
        /// (<c>{ "errors": [ ... ] }</c>). Populated only when
        /// <see cref="Type"/> is <see cref="RestApiErrorType.Http"/> and the
        /// response body matched the cloud error contract. Use
        /// <see cref="RestApiErrorExtensions"/> (<c>HasCode</c>,
        /// <c>GetByCode</c>, <c>FirstCloudError</c>) for ergonomic lookups
        /// instead of indexing this list directly.
        /// </summary>
        public List<CloudApiError> Errors;

        public static RestApiError Validation(string message)
        {
            return new RestApiError
            {
                Type = RestApiErrorType.Validation,
                Message = message
            };
        }
    }
}

