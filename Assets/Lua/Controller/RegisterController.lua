
local RegisterController = {}

local uiMgr = CS.UIManager.Instance
local eventCenter = CS.EventCenter.Instance
local _initDone = false

function RegisterController.Init()
    if _initDone then return end
    _initDone = true
    CS.UnityEngine.Debug.Log("[Lua] RegisterController.Init")
    CS.GameNetwork.Instance:SetRegisterCallback(RegisterController.OnRegisterResponse)
end

function RegisterController.OnRegister(account, password)
    CS.UnityEngine.Debug.Log("[Lua] OnRegister - 账号: " .. account)
    CS.GameNetwork.Instance:SendRegister(account, password)
end

function RegisterController.OnRegisterResponse(rsp)
    if rsp.Result == CS.Game.Proto.RegisterResult.RegisterSuccess then
        CS.UnityEngine.Debug.Log("[Lua] 注册成功! UID: " .. rsp.Uid)
        uiMgr:ClosePanel("RegisterPanel")
        uiMgr:OpenPanelByName("LoginPanel", CS.UIManager.UILayer.Normal)
    elseif rsp.Result == CS.Game.Proto.RegisterResult.RegisterAccountExists then
        eventCenter:FireString("OnShowTip", "账号已存在")
    else
        eventCenter:FireString("OnShowTip", "注册失败")
    end
end

return RegisterController
