using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace Assets.Scripts.Game.Client.GameObjects.Network
{
    [DefaultExecutionOrder(-10000)]
    public class EnsureSingleNetworkManager : MonoBehaviour
    {
        static string PathOf(Transform t)
        {
            var p = t.name;
            while (t.parent) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        void Awake()
        {
            var all = FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var nm in all)
                Debug.Log($"[NM-TRACE] found: scene={nm.gameObject.scene.name}, active={nm.gameObject.activeSelf}, " +
                          $"listening={nm.IsListening}, path={PathOf(nm.transform)}");

            if (all.Length <= 1) return;

            // 현재 씬(InGame 등) + Listening인 것을 우선 보존
            var keep = all
                .OrderByDescending(n => n.IsListening)
                .ThenByDescending(n => n.gameObject.scene == gameObject.scene)
                .First();

            foreach (var nm in all)
                if (nm != keep) Destroy(nm.gameObject);

            Debug.Log($"[EnsureSingleNM] Keep {keep.name} (id={keep.GetInstanceID()}), destroyed {all.Length - 1} duplicates.");
        }
    }
}
