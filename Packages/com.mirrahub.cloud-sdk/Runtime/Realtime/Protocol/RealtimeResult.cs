using System;

namespace MirraCloud.Core.Realtime.Protocol
{
    [Serializable]
    public class RealtimeResult
    {
        public bool IsSuccess;
        public string Code;
        public string Message;

        public static RealtimeResult Success()
        {
            return new RealtimeResult { IsSuccess = true };
        }

        public static RealtimeResult Fail(string code, string message)
        {
            return new RealtimeResult
            {
                IsSuccess = false,
                Code = code,
                Message = message
            };
        }
    }

    [Serializable]
    public sealed class RealtimeResult<T> : RealtimeResult
    {
        public T Data;

        public static RealtimeResult<T> Success(T data)
        {
            return new RealtimeResult<T>
            {
                IsSuccess = true,
                Data = data
            };
        }

        public static new RealtimeResult<T> Fail(string code, string message)
        {
            return new RealtimeResult<T>
            {
                IsSuccess = false,
                Code = code,
                Message = message
            };
        }
    }
}
