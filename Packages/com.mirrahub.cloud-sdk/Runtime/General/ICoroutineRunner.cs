using System.Collections;
using UnityEngine;

namespace MirraCloud
{
    public interface ICoroutineRunner
    {
        Coroutine StartCoroutine(IEnumerator routine);
    }
}
