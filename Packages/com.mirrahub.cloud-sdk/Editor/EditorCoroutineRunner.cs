using System.Collections;
using Unity.EditorCoroutines.Editor;
using UnityEngine;

namespace MirraCloud.Editor
{
    public class EditorCoroutineRunner : ICoroutineRunner
    {
        private readonly object _owner;

        public EditorCoroutineRunner(object owner)
        {
            _owner = owner;
        }

        public Coroutine StartCoroutine(IEnumerator routine)
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(routine);
            return null;
        }
    }
}
