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
        void Awake()
        {
            var all = FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all.Length <= 1) return;

            // 리스닝 중인 걸 우선 유지
            var keep = all.FirstOrDefault(n => n.IsListening) ?? all[0];
            foreach (var nm in all)
                if (nm != keep) Destroy(nm.gameObject);

            DontDestroyOnLoad(keep.gameObject);
            Debug.Log($"[EnsureSingleNM] Keep {keep.name} (id={keep.GetInstanceID()}), destroyed {all.Length - 1} duplicates.");
        }
    }
}
