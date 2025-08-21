using System.Threading.Tasks;
using Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Views
{
    public class UserView : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] UserController _controller;

        [Header("UI (Canvas - User)")]
        [SerializeField] Button _showRegister;

        [Header("UI (Canvas - Register")]
        [SerializeField] Canvas _register;
        [SerializeField] TMP_InputField _registerUsername;
        [SerializeField] TMP_InputField _registerPassword;
        [SerializeField] TMP_InputField _registerPasswordToDoubleCheck;
        [SerializeField] Button _registerConfirm;
        [SerializeField] Button _registerCancel;
        [SerializeField] GameObject _passwordsDifferentAlert;

        [Header("UI (Canvas - Alert)")]
        [SerializeField] Canvas _alert;
        [SerializeField] TMP_Text _alertMessage;


        private void OnEnable()
        {
            _showRegister.onClick.AddListener(OnClickShowRegisterWindow);
            _registerUsername.onValueChanged.AddListener(CheckUsername);
            _registerPasswordToDoubleCheck.onValueChanged.AddListener(DoubleCheckPassword);
            _registerConfirm.onClick.AddListener(OnClickRegisterConfirm);
            _registerCancel.onClick.AddListener(OnClickRegisterCancel);
            CheckRegisterFormatOK();
        }

        private void OnDisable()
        {
            _showRegister.onClick.RemoveListener(OnClickShowRegisterWindow);
            _registerUsername.onValueChanged.RemoveListener(CheckUsername);
            _registerPasswordToDoubleCheck.onValueChanged.RemoveListener(DoubleCheckPassword);
            _registerConfirm.onClick.RemoveListener(OnClickRegisterConfirm);
            _registerCancel.onClick.RemoveListener(OnClickRegisterCancel);
        }

        void OnClickShowRegisterWindow()
        {
            _register.enabled = true;
        }

        /// <summary>
        /// 회원가입 확인 버튼
        /// </summary>
        async void OnClickRegisterConfirm()
        {
            var group = _register.GetComponent<CanvasGroup>();
            group.interactable = false;
            var response = await _controller.RegisterAsync(_registerUsername.text, _registerPassword.text);

            if (response.success)
            {
                _registerUsername.text = string.Empty;
                _registerPassword.text = string.Empty;
                _registerPasswordToDoubleCheck.text = string.Empty;
                _alertMessage.text = "Successfully Registered.";
                _alert.Show();
                await Task.Delay(2000);
                _alert.Hide();
                _register.Hide();
            }
            else
            {
                _alertMessage.text = $"Failed to Registered. {response.message}";
                _alert.Show();
                await Task.Delay(2000);
                _alert.Hide();
            }

            group.interactable = true;
        }

        /// <summary>
        /// 회원가입 취소 버튼
        /// </summary>
        void OnClickRegisterCancel()
        {
            _register.enabled = false;
        }


        void CheckUsername(string value)
        {
            // TODO : 유효한지 체크해서 알람 띄움

            CheckRegisterFormatOK();
        }

        /// <summary>
        /// 비밀번호 확인용 비교해서 다르면 알람 띄움
        /// </summary>
        void DoubleCheckPassword(string toCompare)
        {
            if (_registerPassword.text.Equals(toCompare))
            {
                _passwordsDifferentAlert.SetActive(false);
            }
            else
            {
                _passwordsDifferentAlert.SetActive(true);
            }

            CheckRegisterFormatOK();
        }

        /// <summary>
        /// 회원가입 양식이 올바른지 확인하여 등록 버튼 활성/비활성
        /// </summary>
        void CheckRegisterFormatOK()
        {
            bool isOK = true;

            int usernameLength = _registerUsername.text.Length;

            if (usernameLength < 2 || usernameLength > 16)
                isOK = false;

            int passwordLength = _registerPassword.text.Length;

            if (passwordLength < 6)
                isOK = false;

            if (_registerPassword.text.Equals(_registerPasswordToDoubleCheck.text) == false)
                isOK = false;

            _registerConfirm.interactable = isOK;
        }
    }
}