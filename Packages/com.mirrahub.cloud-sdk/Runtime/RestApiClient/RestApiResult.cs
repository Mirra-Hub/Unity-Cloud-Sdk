using System;

namespace MirraCloud.Core
{
    [Serializable]
    public class RestApiResult
    {
        public bool IsSuccess;
        public RestApiError Error;

        public string Method;
        public string Route;
        public string Url;

        public long? HttpStatusCode;
        public int RetryCount;
        public long DurationMs;

        public string ResponseBody;

        public RestApiResult WithMetaFrom(RestApiResult source)
        {
            if (source == null)
            {
                return this;
            }

            Method = source.Method;
            Route = source.Route;
            Url = source.Url;
            HttpStatusCode = source.HttpStatusCode;
            RetryCount = source.RetryCount;
            DurationMs = source.DurationMs;
            ResponseBody = source.ResponseBody;
            return this;
        }

        public static RestApiResult Success()
        {
            return new RestApiResult { IsSuccess = true };
        }

        public static RestApiResult Fail(RestApiError error)
        {
            return new RestApiResult
            {
                IsSuccess = false,
                Error = error
            };
        }

        public static RestApiResult ValidationFail(string message)
        {
            return Fail(RestApiError.Validation(message));
        }
    }

    [Serializable]
    public sealed class RestApiResult<T> : RestApiResult
    {
        public T Data;

        public new RestApiResult<T> WithMetaFrom(RestApiResult source)
        {
            base.WithMetaFrom(source);
            return this;
        }

        public static RestApiResult<T> Success(T data)
        {
            return new RestApiResult<T>
            {
                IsSuccess = true,
                Data = data
            };
        }

        public static new RestApiResult<T> Fail(RestApiError error)
        {
            return new RestApiResult<T>
            {
                IsSuccess = false,
                Error = error
            };
        }

        public static new RestApiResult<T> ValidationFail(string message)
        {
            return Fail(RestApiError.Validation(message));
        }
    }
}
