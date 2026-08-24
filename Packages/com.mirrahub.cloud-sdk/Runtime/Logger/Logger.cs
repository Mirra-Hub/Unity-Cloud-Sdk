using UnityEngine;

namespace MirraCloud.Core.Logger
{
    public class Logger : ILogger
    {
        public void Log(string message)
        {
            Debug.Log(message);
        }

        public void Error(string message)
        {
            Debug.LogError(message);
        }
    }
}