using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LoginPanel - 登录面板（测试用）
/// 
/// 继承BasePanel，演示生命周期用法
/// </summary>
public class LoginPanel : BasePanel
{
    private Button _loginBtn;
    private Button _registerBtn;
    private InputField _accountInput;
    private InputField _passwordInput;

    public override void OnCreate()
    {
        _loginBtn = Find<Button>("LoginBtn");
        _registerBtn = Find<Button>("RegisterBtn");
        _accountInput = Find<InputField>("AccountInput");
        _passwordInput = Find<InputField>("PasswordInput");

        Debug.Log("[LoginPanel] OnCreate - 组件引用已获取");
    }

    public override void OnShow()
    {
        if (_loginBtn != null)
            _loginBtn.onClick.AddListener(OnLoginClick);
        if (_registerBtn != null)
            _registerBtn.onClick.AddListener(OnRegisterClick);

        LuaManager.Instance.CallFunction("Controller.LoginController", "Init");

        Debug.Log("[LoginPanel] OnShow - 事件已注册");
    }

    public override void OnHide()
    {
        if (_loginBtn != null)
            _loginBtn.onClick.RemoveListener(OnLoginClick);
        if (_registerBtn != null)
            _registerBtn.onClick.RemoveListener(OnRegisterClick);

        

        Debug.Log("[LoginPanel] OnHide - 事件已注销");
    }


    private void OnLoginClick()
    {
        string account = _accountInput != null ? _accountInput.text : "";
        string password = _passwordInput != null ? _passwordInput.text : "";
        
        // 转发到 Lua 执行业务逻辑
        LuaManager.Instance.CallFunction("Controller.LoginController", "OnLogin", account, password);
    }

    private void OnRegisterClick()
    {   
        UIManager.Instance.ClosePanel<LoginPanel>();
        UIManager.Instance.OpenPanel<RegisterPanel>(UIManager.UILayer.Normal);
        
    }
}
