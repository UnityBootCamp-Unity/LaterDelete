using Game.Client.Models;
using System;
using System.Collections;
using UnityEngine;

namespace Game.Client.GameObjects.Characters
{
    public class Player : MonoBehaviour
    {
        public PlayerStatus status { get; private set; }

        public event Action<PlayerStatus, PlayerStatus> onStatusChanged;


        private void Start()
        {
            StartCoroutine(C_TestWorkflow());
        }

        IEnumerator C_TestWorkflow()
        {
            ChangeStatus(new PlayerStatus
            {
                isReady = false,
                isFinished = false,
            });

            yield return new WaitForSeconds(1);

            ChangeStatus(new PlayerStatus
            {
                isReady = true,
                isFinished = false,
            });

            yield return new WaitForSeconds(300);

            ChangeStatus(new PlayerStatus
            {
                isReady = true,
                isFinished = true,
            });
        }

        public void ChangeStatus(PlayerStatus newStatus)
        {
            var oldStatus = status;
            status = newStatus;
            onStatusChanged?.Invoke(oldStatus, newStatus);
        }
    }
}
