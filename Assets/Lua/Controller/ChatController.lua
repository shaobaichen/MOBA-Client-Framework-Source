
local ChatController = {}

local eventCenter = CS.EventCenter.Instance

-- ===== 初始化 =====
function ChatController.Init()
    -- TODO: Debug.Log
    -- TODO: CS.GameNetwork.Instance:SetChatMsgCallback(ChatController.OnChatMsg)
    CS.GameNetwork.Instance:SetChatMsgCallback(ChatController.OnChatMsg)
    -- TODO: CS.GameNetwork.Instance:SetChatHistoryCallback(ChatController.OnChatHistory)
    CS.GameNetwork.Instance:SetChatHistoryCallback(ChatController.OnChatHistory)
end

-- ===== 发送消息 =====
function ChatController.SendMessage(from, to, text)
    -- TODO: CS.GameNetwork.Instance:SendChatMsg(from, to, text)
    CS.GameNetwork.Instance:SendChatMsg(from,to,text)
end

-- ===== 拉取历史 =====
function ChatController.RequestHistory(myAccount, friendAccount)
    -- TODO: CS.GameNetwork.Instance:SendGetChatHistory(myAccount, friendAccount)
    CS.GameNetwork.Instance:SendGetChatHistory(myAccount,friendAccount)
end

-- ===== 收到新消息 =====
function ChatController.OnChatMsg(msg)
    CS.UnityEngine.Debug.Log("[Lua] OnChatMsg called: " .. msg.From .. ": " .. msg.Text)
    eventCenter:FireObject(CS.EventCenter.ON_NEW_CHAT_MESSAGE, msg)
end

-- ===== 收到聊天历史 =====
function ChatController.OnChatHistory(rsp)
    CS.UnityEngine.Debug.Log("[Lua] OnChatHistory called, msgs=" .. rsp.Messages.Count)
    eventCenter:FireObject(CS.EventCenter.ON_CHAT_HISTORY, rsp)
end

-- ===== 注销 =====
function ChatController.UnregisterEvents()
    -- TODO: CS.GameNetwork.Instance:SetChatMsgCallback(nil)
    CS.GameNetwork.Instance:SetChatMsgCallback(nil)
    -- TODO: CS.GameNetwork.Instance:SetChatHistoryCallback(nil)
    CS.GameNetwork.Instance:SetChatHistoryCallback(nil)
end

return ChatController
