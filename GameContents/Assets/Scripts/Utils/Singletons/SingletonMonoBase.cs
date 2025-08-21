using UnityEngine;

namespace Utils.Singletons
{
    public class SingletonMonoBase<T> : MonoBehaviour
        where T : SingletonMonoBase<T>
    {
        public static T instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (s_gate)
                    {
                        if (_instance == null)
                        {
                            _instance = new GameObject(typeof(T).Name).AddComponent<T>();
                            DontDestroyOnLoad(_instance);
                        }
                    }
                }

                return _instance;
            }
        }

        private static volatile T _instance;
        private static readonly object s_gate = new object();


        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
            }
            else
            {
                _instance = (T)this;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
