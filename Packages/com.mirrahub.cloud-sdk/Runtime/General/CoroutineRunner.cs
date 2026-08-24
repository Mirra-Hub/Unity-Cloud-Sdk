using UnityEngine;

namespace MirraCloud
{
    public class CoroutineRunner : MonoBehaviour, ICoroutineRunner
    {
        public static CoroutineRunner CreateInstance()
        {
            GameObject obj = new GameObject("MirraCloudSDK Coroutine Runner");

            var coroutineRunner = obj.AddComponent<CoroutineRunner>();

            DontDestroyOnLoad(obj);

            return coroutineRunner;
        }
    }
}
