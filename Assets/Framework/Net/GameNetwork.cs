using System;
using Game.Proto;
using Google.Protobuf;
using UnityEngine;
using XLua;

public class GameNetwork : MonoBehaviour
{
    public static GameNetwork Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[GameNetwork]");
                _instance = go.AddComponent<GameNetwork>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    private static GameNetwork _instance;

    private LuaFunction _luaOnLoginResponse;
    private LuaFunction _luaOnRegisterResponse;
    private LuaFunction _luaOnAddFriendResponse;
    private LuaFunction _luaOnFriendListResponse;
    private LuaFunction _luaOnFriendStatusNotify;  
    private LuaFunction _luaOnFriendRequestNotify;   // 收到申请通知的回调（给被申请人）                                                    //收到申请通知的回调(给被申请人)
    private LuaFunction _luaOnRespondFriendRequestRsp; // 申请结果回调（给申请人）

    private LuaFunction _luaOnDeleteFriendRsp; //删除好友回调
    

    private LuaFunction _luaOnChatMsg;
    private LuaFunction _luaOnChatHistory;
   
    private bool _initialized;

    private void Awake()
    {
        Init();
    }

    public void Init()
    {
        if (_initialized) return;
        _initialized = true;

        MessageRouter.Instance.Register(MessageId.S2C_LoginRsp, OnRawLoginResponse);
        MessageRouter.Instance.Register(MessageId.S2C_RegisterRsp, OnRawRegisterResponse);
        MessageRouter.Instance.Register(MessageId.S2C_AddFriendRsp, OnRawAddFriendResponse);
        MessageRouter.Instance.Register(MessageId.S2C_GetFriendListRsp, OnRawFriendListResponse);
        MessageRouter.Instance.Register(MessageId.S2C_FriendStatusNotify, OnRawFriendStatusNotify);
        MessageRouter.Instance.Register(MessageId.S2C_FriendRequestNotify,OnRawFriendRequestNotify);
        MessageRouter.Instance.Register(MessageId.S2C_RespondFriendRequestRsp,OnRawRespondFriendRequestRsp);
        MessageRouter.Instance.Register(MessageId.S2C_DeleteFriendRsp,OnRawDeleteFriendRsp);

        MessageRouter.Instance.Register(MessageId.S2C_ChatMsg,OnRawChatMsg);
        MessageRouter.Instance.Register(MessageId.S2C_GetChatHistoryRsp, OnRawChatHistoryRsp);



    }

    


    public void SetLoginCallback(LuaFunction func) { _luaOnLoginResponse = func; } //登录
    public void SetRegisterCallback(LuaFunction func) { _luaOnRegisterResponse = func; } //注册
    public void SetAddFriendCallback(LuaFunction func) { _luaOnAddFriendResponse = func; } //加好友
    public void SetFriendListCallback(LuaFunction func) { _luaOnFriendListResponse = func; }//设置好友列表
    public void SetFriendStatusCallback(LuaFunction func) { _luaOnFriendStatusNotify = func; }

    public void SetFriendRequestNotifyCallback(LuaFunction func){_luaOnFriendRequestNotify = func;} //请求加好友
    public void SetRespondFriendRequestCallback(LuaFunction func) { _luaOnRespondFriendRequestRsp = func; }
    
    public void SetDeleteFriendCallback(LuaFunction func){_luaOnDeleteFriendRsp = func;}

    public void SetChatMsgCallback(LuaFunction func){_luaOnChatMsg = func;} //发送消息
    public void SetChatHistoryCallback(LuaFunction func){_luaOnChatHistory = func;} //调历史消息


#region  发送
        public void SendLogin(string account, string password)
    {
        var req = new C2S_LoginReq { Account = account, PasswordHash = password };
        byte[] bytes = req.ToByteArray();
        MessageRouter.Instance.Send(MessageId.C2S_LoginReq, bytes);
    }

    public void SendRegister(string account, string password)
    {
        var req = new C2S_RegisterReq { Account = account, PasswordHash = password };
        byte[] bytes = req.ToByteArray();
        MessageRouter.Instance.Send(MessageId.C2S_RegisterReq, bytes);
    }

    public void SendAddFriend(string myAccount, string targetAccount)
    {
        var req = new C2S_AddFriend { MyAccount = myAccount, TargetAccount = targetAccount };
        byte[] bytes = req.ToByteArray();
        MessageRouter.Instance.Send(MessageId.C2S_AddFriend, bytes);
    }

    public void SendGetFriendList(string myAccount)
    {
        var req = new C2S_GetFriendList { MyAccount = myAccount };
        byte[] bytes = req.ToByteArray();
        MessageRouter.Instance.Send(MessageId.C2S_GetFriendList, bytes);
    }

    public void SendFriendRequest(string from,string to)
    {
        var req = new C2S_SendFriendRequest
        {
            From = from,
            To = to,         
        };
        byte[] bytes = req.ToByteArray();
        MessageRouter.Instance.Send(MessageId.C2S_SendFriendRequest,bytes);
    }
    public void RespondFriendRequest(string myAccount,string fromAccount,bool accept)
    {
        var req = new C2S_RespondFriendRequest
        {
            MyAccount = myAccount,
            FromAccount = fromAccount,
            Accept = accept,
        };
        byte[] bytes = req.ToByteArray();
        MessageRouter.Instance.Send(MessageId.C2S_RespondFriendRequest,bytes);
    }

    public void SendDeleteFriend(string myAccount,string FriendAccount)
    {
        var req = new C2S_DeleteFriend
        {
            MyAccount = myAccount,
            FriendAccount = FriendAccount
        };
        byte[] bytes = req.ToByteArray();
        MessageRouter.Instance.Send(MessageId.C2S_DeleteFriend,bytes);
    }
    public void SendChatMsg(string from,string to,string text)
    {
        var req = new C2S_SendChatMsg
        {
            From = from,
            To = to,
            Text = text,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

        };
        byte[] bytes = req.ToByteArray();
        MessageRouter.Instance.Send(MessageId.C2S_SendChatMsg,bytes);

    }

    public void SendGetChatHistory(string MyAccount,string FriendAccount) 
    {
        var req = new C2S_GetChatHistory { MyAccount = MyAccount, FriendAccount = FriendAccount };

        byte[] bytes = req.ToByteArray();
        MessageRouter.Instance.Send(MessageId.C2S_GetChatHistory,bytes);
    }
#endregion



#region 接收
        private void OnRawLoginResponse(byte[] data)
    {
        try
        {
            var rsp = S2C_LoginRsp.Parser.ParseFrom(data);
            _luaOnLoginResponse?.Call(rsp);
        }
        catch (Exception ex)
        {
            Debug.LogError("[GameNetwork] 登录响应处理失败: " + ex.Message);
        }
    }

    private void OnRawRegisterResponse(byte[] data)
    {
        try
        {
            var rsp = S2C_RegisterRsp.Parser.ParseFrom(data);
            _luaOnRegisterResponse?.Call(rsp);
        }
        catch (Exception ex)
        {
            Debug.LogError("[GameNetwork] 注册响应处理失败: " + ex.Message);
        }
    }

    private void OnRawAddFriendResponse(byte[] data)
    {
        try
        {
            var rsp = S2C_AddFriendRsp.Parser.ParseFrom(data);
            _luaOnAddFriendResponse?.Call(rsp);
        }
        catch (Exception ex)
        {
            Debug.LogError("[GameNetwork] 添加好友响应处理失败: " + ex.Message);
        }
    }

    private void OnRawFriendListResponse(byte[] data)
    {
        try
        {
            var rsp = S2C_FriendList.Parser.ParseFrom(data);
            _luaOnFriendListResponse?.Call(rsp);
        }
        catch (Exception ex)
        {
            Debug.LogError("[GameNetwork] 好友列表响应处理失败: " + ex.Message);
        }
    }

    private void OnRawFriendStatusNotify(byte[] data)
    {
        try
        {
            var notify = S2C_FriendStatusNotify.Parser.ParseFrom(data);
            _luaOnFriendStatusNotify?.Call(notify);
        }
        catch (Exception ex)
        {
            Debug.LogError("[GameNetwork] 好友状态推送处理失败: " + ex.Message);
        }
    }

    private void OnRawFriendRequestNotify(byte[] data)
    {
        try
        {
            var notify = S2C_FriendRequestNotify.Parser.ParseFrom(data);
            _luaOnFriendRequestNotify?.Call(notify);
        }
        catch (System.Exception ex)
        {
            
            Debug.LogError("[GameNetwork] 好友申请通知处理失败: " + ex.Message);
        }
    }
    private void OnRawRespondFriendRequestRsp(byte[] data)
    {
        try
        {
            var rsp = S2C_RespondFriendRequestRsp.Parser.ParseFrom(data);
            _luaOnRespondFriendRequestRsp?.Call(rsp);
        }
        catch (Exception ex)
        {
            Debug.LogError("[GameNetwork] 好友申请响应处理失败: " + ex.Message);
        }
        
    }

    private void OnRawDeleteFriendRsp(byte[] data)
    {
        try
        {
            var rsp = S2C_DeleteFriendRsp.Parser.ParseFrom(data);
            _luaOnDeleteFriendRsp?.Call(rsp);
        }
        catch (System.Exception ex)
        {
            
            Debug.LogError("[GameNetwork] 好友删除响应处理失败: " + ex.Message);
        }
    }
    
    private void OnRawChatMsg(byte[] data)
    {
        try
        {
            var msg = S2C_ChatMsg.Parser.ParseFrom(data);
            _luaOnChatMsg?.Call(msg);
        }
        catch (Exception ex)
        {
            Debug.LogError("[GameNetwork] 聊天消息处理失败: " + ex.Message);
        }
    }

    private void OnRawChatHistoryRsp(byte[] data) 
    {
         try
        {
            var rsp = S2C_ChatHistory.Parser.ParseFrom(data);
            _luaOnChatHistory?.Call(rsp);
        }
        catch (Exception ex)
        {
            Debug.LogError("[GameNetwork] 聊天消息处理失败: " + ex.Message);
        }
    }
#endregion


   

    private void OnDestroy()
    {
        _luaOnLoginResponse?.Dispose();
        _luaOnRegisterResponse?.Dispose();
        _luaOnAddFriendResponse?.Dispose();
        _luaOnFriendListResponse?.Dispose();
        _luaOnFriendStatusNotify?.Dispose();
        _luaOnChatMsg?.Dispose();
        _luaOnChatHistory?.Dispose();
        _luaOnFriendRequestNotify?.Dispose();
        _luaOnRespondFriendRequestRsp?.Dispose();
        _luaOnDeleteFriendRsp?.Dispose();
        
        

        if (MessageRouter.Instance != null)
        {
            MessageRouter.Instance.Unregister(MessageId.S2C_LoginRsp, OnRawLoginResponse);
            MessageRouter.Instance.Unregister(MessageId.S2C_RegisterRsp, OnRawRegisterResponse);
            MessageRouter.Instance.Unregister(MessageId.S2C_AddFriendRsp, OnRawAddFriendResponse);
            MessageRouter.Instance.Unregister(MessageId.S2C_GetFriendListRsp, OnRawFriendListResponse);
            MessageRouter.Instance.Unregister(MessageId.S2C_FriendStatusNotify, OnRawFriendStatusNotify);
            MessageRouter.Instance.Unregister(MessageId.S2C_ChatMsg,OnRawChatMsg);
            MessageRouter.Instance.Unregister(MessageId.S2C_GetChatHistoryRsp, OnRawChatHistoryRsp);
            MessageRouter.Instance.Unregister(MessageId.S2C_FriendRequestNotify,OnRawFriendRequestNotify);
            MessageRouter.Instance.Unregister(MessageId.S2C_RespondFriendRequestRsp,OnRawRespondFriendRequestRsp);
            MessageRouter.Instance.Unregister(MessageId.S2C_DeleteFriendRsp,OnRawDeleteFriendRsp);
        }
        _instance = null;
    }
}
