using Game.Client.Controllers;
using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Game.Client.Views
{
    public class ChatView : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] ChatController _controller;

        [Header("UI")]
        [SerializeField] TMP_InputField _input;
        [SerializeField] TMP_Text _messages;

        private void OnEnable()
        {
            _input.onSubmit.AddListener(Send);
            _controller.onMessageReceived += OnMessageReceived;
        }

        private void OnDisable()
        {
            _input.onSubmit.RemoveListener(Send);
            _controller.onMessageReceived -= OnMessageReceived;
        }

        private async void Send(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            _input.interactable = false;
            await _controller.SendAsync(text);
            _input.text = string.Empty;
            _input.interactable = true;
        }

        private void OnMessageReceived((int senderId, int receiverId, DateTime time, string content) chatMessage)
        {
            _messages.text += $"[{chatMessage.senderId}] {chatMessage.content}\n";
        }
    }
}
