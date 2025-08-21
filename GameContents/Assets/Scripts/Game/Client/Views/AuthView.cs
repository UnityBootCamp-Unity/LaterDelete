using Game.Client.Controllers;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils;

namespace Game.Client.Views
{
    public class AuthView : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] AuthController _controller;

        [Header("UI (Canvas - Login)")]
        [SerializeField] TMP_InputField _loginUsername;
        [SerializeField] TMP_InputField _loginPassword;
        [SerializeField] Button _loginConfirm;

        [Header("UI (Canvas - Alert)")]
        [SerializeField] Canvas _alert;
        [SerializeField] TMP_Text _alertMessage;


        private void OnEnable()
        {
            _loginConfirm.onClick.AddListener(OnConfirmButtonClicked);
        }

        private void OnDisable()
        {
            _loginConfirm.onClick.RemoveListener(OnConfirmButtonClicked);
        }

        
        private async void OnConfirmButtonClicked()
        {
            SetInteractable(false);
            var (success, message) = await _controller.LoginAsync(_loginUsername.text, _loginPassword.text);

            if (success)
            {
                _alertMessage.text = "Loged in.";
                _alert.Show();
            }
            else
            {
                _alertMessage.text = $"Failed to login. \n {message}";
                _alert.Show();
                await Task.Delay(2000);
                _alert.Hide();
                SetInteractable(true);
            }
        }

        /// <summary>
        /// 웹 응답 받을때까지는 유저가 UI 와 상호작용을 하면 안된다.
        /// </summary>
        void SetInteractable(bool value)
        {
            _loginUsername.interactable = value;
            _loginPassword.interactable = value;
            _loginConfirm.interactable= value;
        }
    }
}