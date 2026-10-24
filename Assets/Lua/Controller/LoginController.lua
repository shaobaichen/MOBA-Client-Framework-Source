
local LoginController = {}

local uiMgr = CS.UIManager.Instance
local eventCenter = CS.EventCenter.Instance

LoginController.Account = ""
LoginController.SessionToken = ""
local _initDone = false

function LoginController.Init()
    if _initDone then return end
    _initDone = true
    CS.UnityEngine.Debug.Log("[Lua] LoginController.Init")
    CS.GameNetwork.Instance:SetLoginCallback(LoginController.OnLoginResponse)
end

function LoginController.OnLogin(account, password)
    LoginController.Account = account
    CS.UnityEngine.Debug.Log("[Lua] OnLogin - 账号: " .. account)
    LoginController.SendLoginRequest(account, password)
end

function LoginController.SendLoginRequest(account, password)
    CS.UnityEngine.Debug.Log("[Lua] 发送登录请求...")
    CS.GameNetwork.Instance:SendLogin(account, password)
end

function LoginController.OnLoginResponse(rsp)
    if rsp.Result == CS.Game.Proto.LoginResult.LoginSuccess then
        LoginController.SessionToken = rsp.SessionToken
        CS.DataManager.Instance:SetPlayerData(rsp.PlayerData, LoginController.Account)
        local nickname = rsp.PlayerData and rsp.PlayerData.Nickname or LoginController.Account
        LoginController.OnLoginSuccess(nickname)
    elseif rsp.Result == CS.Game.Proto.LoginResult.LoginAccountNotExist then
        eventCenter:FireString("OnShowTip", "账号不存在")
    elseif rsp.Result == CS.Game.Proto.LoginResult.LoginWrongPassword then
        eventCenter:FireString("OnShowTip", "密码错误")
    elseif rsp.Result == CS.Game.Proto.LoginResult.LoginBanned then
        eventCenter:FireString("OnShowTip", "账号已被封禁")
    end
end

function LoginController.OnLoginSuccess(nickname)
    CS.UnityEngine.Debug.Log("[Lua] 登录成功！欢迎: " .. nickname)
    eventCenter:FireString(CS.EventCenter.ON_LOGIN_SUCCESS, nickname)
    uiMgr:OpenPanelByName("HallPanel", CS.UIManager.UILayer.Normal)
    eventCenter:FireString("OnShowTip", "热更新V2.0.0 验证热更新逻辑热更新")
end

function LoginController.UnregisterEvents()
    CS.GameNetwork.Instance:SetLoginCallback(nil)
    CS.UnityEngine.Debug.Log("[Lua] LoginController 事件已注销")
end

return LoginController
