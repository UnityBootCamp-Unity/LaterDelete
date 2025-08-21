using UnityEngine;

namespace Utils
{
    public static class BehaviourExtensions
    {
        public static void Show(this Behaviour behaviour)
        {
            behaviour.enabled = true;
        }

        public static void Hide(this Behaviour behaviour)
        {
            behaviour.enabled = false;
        }
    }
}
