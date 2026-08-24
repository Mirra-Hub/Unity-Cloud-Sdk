using System;
using System.Collections.Generic;

namespace MirraCloud.Core
{
    /// <summary>
    /// Простой менеджер интерсепторов (по аналогии с axios).
    /// </summary>
    public class InterceptorManager<T>
    {
        private int _nextId = 0;
        private readonly Dictionary<int, Func<T, System.Collections.IEnumerator>> _interceptors = new();

        public int Use(Func<T, System.Collections.IEnumerator> interceptor)
        {
            var id = ++_nextId;
            _interceptors[id] = interceptor;
            return id;
        }

        public void Eject(int id)
        {
            _interceptors.Remove(id);
        }

        public void Clear()
        {
            _interceptors.Clear();
        }

        public IEnumerable<Func<T, System.Collections.IEnumerator>> GetAll()
        {
            return _interceptors.Values;
        }
    }
}

