using UnityEngine;
using UnityEngine.UI;

public class RegisterPanel : BasePanel
{
    private InputField _accountInput;
    private InputField _passwordInput;
    private InputField _confirmInput;
    private Button _registerBtn;
    private Button _backBtn;
    private Text _tipText;

    public override void OnCreate()
    {
        _accountInput = Find<InputField>("AccountInput");
        _passwordInput = Find<InputField>("PasswordInput");
        _confirmInput = Find<InputField>("ConfirmInput");
        _registerBtn = Find<Button>("RegisterBtn");
        _backBtn = Find<Button>("BackBtn");
        _tipText = Find<Text>("TipText");

        if (_tipText != null)
            _tipText.text = "";
    }

    public override void OnShow()
    {
        if (_registerBtn != null)
            _registerBtn.onClick.AddListener(OnRegisterClick);
        if (_backBtn != null)
            _backBtn.onClick.AddListener(OnBackClick);

        LuaManager.Instance.CallFunction("Controller.RegisterController", "Init");
    }

    public override void OnHide()
    {
        if (_registerBtn != null)
            _registerBtn.onClick.RemoveListener(OnRegisterClick);
        if (_backBtn != null)
            _backBtn.onClick.RemoveListener(OnBackClick);
    }

    private void OnRegisterClick()
    {
        string account = _accountInput?.text ?? "";
        string password = _passwordInput?.text ?? "";
        string confirm = _confirmInput?.text ?? "";

        if (string.IsNullOrEmpty(account))
        {
            ShowTip("请输入账号");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowTip("请输入密码");
            return;
        }

        if (password.Length < 6)
        {
            ShowTip("密码长度不能少于6位");
            return;
        }

        if (password != confirm)
        {
            ShowTip("两次密码不一致");
            return;
        }

        ShowTip("注册中...");

        LuaManager.Instance.CallFunction("Controller.RegisterController", "OnRegister", account, password);
    }

    private void OnBackClick()
    {
        UIManager.Instance.ClosePanel<RegisterPanel>();
        UIManager.Instance.OpenPanel<LoginPanel>(UIManager.UILayer.Normal);
    }
    private void ShowTip(string msg)
    {
        if (_tipText != null)
            _tipText.text = msg;
        else
            EventCenter.Instance.FireString("OnShowTip", msg);
    }
}
