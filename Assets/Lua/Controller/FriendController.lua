
local FriendController = {}

local uiMgr = CS.UIManager.Instance
local eventCenter = CS.EventCenter.Instance

function FriendController.Init()
    CS.UnityEngine.Debug.Log("[Lua] FriendController.Init")
    CS.GameNetwork.Instance:SetAddFriendCallback(FriendController.OnAddFriendResponse)
    CS.GameNetwork.Instance:SetFriendListCallback(FriendController.OnFriendListResponse)
    CS.GameNetwork.Instance:SetFriendStatusCallback(FriendController.OnFriendStatusNotify)
    CS.GameNetwork.Instance:SetFriendRequestNotifyCallback(FriendController.OnFriendRequestNotify)
    CS.GameNetwork.Instance:SetRespondFriendRequestCallback(FriendController.OnRespondFriendRequestRsp)
    CS.GameNetwork.Instance:SetDeleteFriendCallback(FriendController.OnDeleteFriendRsp)
end

function FriendController.RefreshList()
    local account = CS.DataManager.Instance.CurrentAccount
    CS.GameNetwork.Instance:SendGetFriendList(account)
end

function FriendController.AddFriend(targetAccount)
    local myAccount = CS.DataManager.Instance.CurrentAccount
    CS.GameNetwork.Instance:SendAddFriend(myAccount, targetAccount)
end

function FriendController.OnAddFriendResponse(rsp)
    if rsp.Result == CS.Game.Proto.AddFriendResult.AddFriendSuccess then
        CS.UnityEngine.Debug.Log("[Lua] 添加好友成功")
        FriendController.RefreshList()
    elseif rsp.Result == CS.Game.Proto.AddFriendResult.AddFriendNotFound then
        eventCenter:FireString("OnShowTip", "对方账号不存在")
    elseif rsp.Result == CS.Game.Proto.AddFriendResult.AddFriendAlreadyExists then
        eventCenter:FireString("OnShowTip", "已经是好友")
    elseif rsp.Result == CS.Game.Proto.AddFriendResult.AddFriendSelf then
        eventCenter:FireString("OnShowTip", "不能添加自己")
    end
end

function FriendController.OnFriendListResponse(rsp)
    CS.UnityEngine.Debug.Log("[Lua] 收到好友列表, 共" .. rsp.Friends.Count .. "人")
    eventCenter:FireObject(CS.EventCenter.ON_FRIEND_LIST_UPDATED, rsp)
end

function FriendController.OnFriendStatusNotify(notify)
    CS.UnityEngine.Debug.Log("[Lua] 好友状态变更: " .. notify.Account .. " → " .. tostring(notify.Online))
    eventCenter:FireObject(CS.EventCenter.ON_FRIEND_STATUS_CHANGED, notify)
end

--发送好友申请
function FriendController.SendFriendRequest(targetAccount)
    local myAccount = CS.DataManager.Instance.CurrentAccount
    CS.GameNetwork.Instance:SendFriendRequest(myAccount, targetAccount)
    eventCenter:FireString("OnShowTip", "已发送申请")
end

--响应申请(同意/拒绝)
function FriendController.RespondFriendRequest(fromAccount, accept)
    local myAccount = CS.DataManager.Instance.CurrentAccount
    CS.GameNetwork.Instance:RespondFriendRequest(myAccount, fromAccount, accept)
    FriendController.RefreshList()
end

function FriendController.OnFriendRequestNotify(notify)
    CS.UnityEngine.Debug.Log("[Lua] 收到好友申请: " .. notify.From)
    CS.GameManager.Instance:ShowConfirmPanel(
        notify.FromNickname .. " 请求添加你为好友",
        notify.From,
        notify.FromNickname
    )
end

function FriendController.OnRespondFriendRequestRsp(rsp)
    if rsp.Result == CS.Game.Proto.FriendRequestResult.FriendRequestAccept then
        eventCenter:FireString("OnShowTip", "对方同意了你的好友申请")
        FriendController.RefreshList()
    else
        eventCenter:FireString("OnShowTip", "对方拒绝了你的好友申请")
    end
end

function FriendController.DeleteFriend(targetAccount)
    local myAccount = CS.DataManager.Instance.CurrentAccount
    CS.GameNetwork.Instance:SendDeleteFriend(myAccount,targetAccount)
    FriendController.RefreshList()
end

function FriendController.OnDeleteFriendRsp(rsp)
    FriendController.RefreshList()
    
end

function FriendController.UnregisterEvents()
    CS.GameNetwork.Instance:SetAddFriendCallback(nil)
    CS.GameNetwork.Instance:SetFriendListCallback(nil)
    CS.GameNetwork.Instance:SetFriendStatusCallback(nil)
    CS.GameNetwork.Instance:SetFriendRequestNotifyCallback(nil)
    CS.GameNetwork.Instance:SetRespondFriendRequestCallback(nil)
    CS.GameNetwork.Instance:SetDeleteFriendCallback(nil)
end

return FriendController
