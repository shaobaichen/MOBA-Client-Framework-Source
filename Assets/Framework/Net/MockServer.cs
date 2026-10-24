using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using Game.Proto;
using Google.Protobuf;
using UnityEngine;

public class MockServer : MonoBehaviour
{
    public static MockServer Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[MockServer]");
                _instance = go.AddComponent<MockServer>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    private static MockServer _instance;

    [System.Serializable]
    private class UserRecord
    {
        public string Account;
        public string PasswordHash;
        public uint Uid;
        public string Nickname;
        public int Level;
        public int AvatarId;
    }

    [Header("Port")]
    public int Port = 8888;

    private TcpListener _listener;
    private Thread _listenThread;
    private bool _isRunning;
    private readonly List<TcpClient> _clients = new List<TcpClient>();
    private readonly object _lock = new object();
    private readonly Queue<string> _debugLogs = new Queue<string>();
    private const int MAX_LOGS = 50;

    private Dictionary<string, UserRecord> _users = new Dictionary<string, UserRecord>();
    private uint _nextUid = 10001;
    private string _userFilePath;
    private Dictionary<string, List<string>> _friendLists = new Dictionary<string, List<string>>();
    private string _friendListFilePath;
    private Dictionary<TcpClient, string> _connectionMap = new Dictionary<TcpClient, string>();
    private Dictionary<string, TcpClient> _accountToClient = new Dictionary<string, TcpClient>();
    private Dictionary<string, List<string>> _reverseFriends = new Dictionary<string, List<string>>();
    private HashSet<string> _onlineUsers = new HashSet<string>();
    private Dictionary<string,List<string>> _pendingRequests = new Dictionary<string, List<string>>();
    private Dictionary<string, List<ChatMsg>> _chatHistory = new Dictionary<string, List<ChatMsg>>();
    private void Awake()
    {
        _userFilePath = Application.persistentDataPath + "/users.json";
        _friendListFilePath = Application.persistentDataPath + "/friends.json";
        LoadUsers();
        LoadFriendLists();
        StartServer();
    }

    public void StartServer()
    {
        if (_isRunning) return;
        try
        {
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            _isRunning = true;
            _listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "MockServer-Listen" };
            _listenThread.Start();
            Log("[MockServer] Start OK, port " + Port);
        }
        catch (Exception ex) { Log("[MockServer] Start fail: " + ex.Message); }
    }

    private void ListenLoop()
    {
        while (_isRunning)
        {
            try
            {
                TcpClient client = _listener.AcceptTcpClient();
                Log("[MockServer] New client: " + client.Client.RemoteEndPoint);
                lock (_lock) { _clients.Add(client); }
                Thread clientThread = new Thread(() => HandleClient(client)) { IsBackground = true, Name = "MockServer-Client" };
                clientThread.Start();
            }
            catch (SocketException) { break; }
            catch (Exception ex) { Log("[MockServer] Accept error: " + ex.Message); }
        }
    }
    private void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] cache = new byte[64 * 1024];
        int cacheLen = 0;
        try
        {
            while (_isRunning && client.Connected)
            {
                int bytesRead = stream.Read(cache, cacheLen, cache.Length - cacheLen);
                if (bytesRead <= 0) break;
                cacheLen += bytesRead;
                while (cacheLen >= 4)
                {
                    int bodyLen = BitConverter.ToInt32(cache, 0);
                    if (bodyLen < 0 || bodyLen > 10 * 1024 * 1024) { Log("[MockServer] Bad bodyLen " + bodyLen); return; }
                    if (cacheLen < 4 + bodyLen) break;
                    if (bodyLen == 0)
                    {
                        int leftover = cacheLen - 4;
                        if (leftover > 0) Buffer.BlockCopy(cache, 4, cache, 0, leftover);
                        cacheLen = leftover;
                        continue;
                    }
                    byte[] body = new byte[bodyLen];
                    Buffer.BlockCopy(cache, 4, body, 0, bodyLen);
                    byte[] response = ProcessMessage(body, client);
                    if (response != null) { stream.Write(response, 0, response.Length); stream.Flush(); }
                    int remain = cacheLen - 4 - bodyLen;
                    if (remain > 0) Buffer.BlockCopy(cache, 4 + bodyLen, cache, 0, remain);
                    cacheLen = remain;
                }
            }
        }
        catch (Exception ex) { Log("[MockServer] Client disconnect: " + ex.Message); }
        finally
        {
            if (_connectionMap.TryGetValue(client, out string account))
            {
                _onlineUsers.Remove(account);
                _connectionMap.Remove(client);
                _accountToClient.Remove(account);
                NotifyFriendStatus(account, false);
            }
            lock (_lock) { _clients.Remove(client); }
            client.Close();
        }
    }

    private void LoadUsers()
    {
        try
        {
            if (!File.Exists(_userFilePath)) return;
            string json = File.ReadAllText(_userFilePath);
            Dictionary<string, object> root = MiniJSON.Json.Deserialize(json) as Dictionary<string, object>;
            if (root == null) return;
            if (root.TryGetValue("nextUid", out object uidObj)) _nextUid = (uint)(long)uidObj;
            if (root.TryGetValue("users", out object usersObj) && usersObj is List<object> list)
            {
                foreach (object item in list)
                {
                    Dictionary<string, object> dict = item as Dictionary<string, object>;
                    if (dict == null) continue;
                    UserRecord rec = new UserRecord
                    {
                        Account = dict["Account"].ToString(),
                        PasswordHash = dict["PasswordHash"].ToString(),
                        Uid = (uint)(long)dict["Uid"],
                        Nickname = dict["Nickname"].ToString(),
                        Level = (int)(long)dict["Level"],
                        AvatarId = (int)(long)dict["AvatarId"]
                    };
                    _users[rec.Account] = rec;
                }
            }
            Log("[MockServer] Loaded " + _users.Count + " users");
        }
        catch (Exception ex) { Log("[MockServer] LoadUsers fail: " + ex.Message); }
    }

    private void SaveUsers()
    {
        try
        {
            List<object> list = new List<object>();
            foreach (UserRecord rec in _users.Values)
            {
                list.Add(new Dictionary<string, object> { { "Account", rec.Account }, { "PasswordHash", rec.PasswordHash }, { "Uid", rec.Uid }, { "Nickname", rec.Nickname }, { "Level", rec.Level }, { "AvatarId", rec.AvatarId } });
            }
            string json = MiniJSON.Json.Serialize(new Dictionary<string, object> { { "nextUid", _nextUid }, { "users", list } });
            File.WriteAllText(_userFilePath, json);
        }
        catch (Exception ex) { Log("[MockServer] SaveUsers fail: " + ex.Message); }
    }
    private byte[] ProcessMessage(byte[] body, TcpClient client)
    {
        if (body.Length < 4) return null;
        int msgId = BitConverter.ToInt32(body, 0);
        byte[] protoBody = new byte[body.Length - 4];
        Buffer.BlockCopy(body, 4, protoBody, 0, protoBody.Length);
        switch (msgId)
        {
            case 2000: return HandleLoginReq(protoBody, client);
            case 2100: return HandleRegisterReq(protoBody);
            case 1000: return HandleHeartbeat(protoBody);
            case 3000: return HandleAddFriend(protoBody);
            case 3100: return HandleGetFriendList(protoBody);
            case 3300: return HandleSendFriendRequest(protoBody);
            case 3400: return HandleRespondFriendRequest(protoBody);
            case 3500: return HandleDeleteFriend(protoBody);
            case 4000: return HandleSendChatMsg(protoBody);
            case 4100: return HandleGetChatHistory(protoBody);

            
            default: Log("[MockServer] Unknown msgId: " + msgId); return null;
        }
    }

    





    /// <summary>
    /// 处理登录
    /// </summary>
    /// <param name="protoBody"></param>
    /// <param name="client"></param>
    /// <returns></returns> <summary>
    /// 
    /// </summary>
    /// <param name="protoBody"></param>
    /// <param name="client"></param>
    /// <returns></returns>
    private byte[] HandleLoginReq(byte[] protoBody, TcpClient client)
    {
        C2S_LoginReq req = C2S_LoginReq.Parser.ParseFrom(protoBody);
        Log("[MockServer] Login: " + req.Account);
        S2C_LoginRsp rsp = new S2C_LoginRsp();
        if (!_users.TryGetValue(req.Account, out UserRecord rec))
            rsp.Result = LoginResult.LoginAccountNotExist;
        else if (rec.PasswordHash != req.PasswordHash)
            rsp.Result = LoginResult.LoginWrongPassword;
        else
        {
            rsp.Result = LoginResult.LoginSuccess;
            rsp.SessionToken = "mock-session-" + Guid.NewGuid().ToString().Substring(0, 8);
            rsp.PlayerData = new PlayerData { Uid = rec.Uid, Nickname = rec.Nickname, Level = rec.Level, AvatarId = rec.AvatarId, Gold = 99999, Diamond = 888 };
            rsp.ServerTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _connectionMap[client] = req.Account;
            _accountToClient[req.Account] = client;
            _onlineUsers.Add(req.Account);
            NotifyFriendStatus(req.Account, true);
        }
        Log("[MockServer] Login result=" + rsp.Result);
        return BuildResponse(2001, rsp.ToByteArray());
    }

    /// <summary>
    /// 处理注册
    /// </summary>
    /// <param name="protoBody"></param>
    /// <returns></returns>
    private byte[] HandleRegisterReq(byte[] protoBody)
    {
        C2S_RegisterReq req = C2S_RegisterReq.Parser.ParseFrom(protoBody);
        Log("[MockServer] Register: " + req.Account);
        S2C_RegisterRsp rsp = new S2C_RegisterRsp();
        if (_users.ContainsKey(req.Account))
            rsp.Result = RegisterResult.RegisterAccountExists;
        else
        {
            UserRecord rec = new UserRecord { Uid = _nextUid++, Account = req.Account, PasswordHash = req.PasswordHash, Nickname = req.Account, Level = 1, AvatarId = 1 };
            _users[req.Account] = rec;
            _friendLists[req.Account] = new List<string>();
            rsp.Result = RegisterResult.RegisterSuccess;
            rsp.Uid = rec.Uid;
            SaveUsers();
        }
        return BuildResponse(2101, rsp.ToByteArray());
    }

    /// <summary>
    /// 处理加好友
    /// </summary>
    /// <param name="protoBody"></param>
    /// <returns></returns>
    private byte[] HandleAddFriend(byte[] protoBody)
    {
        C2S_AddFriend req = C2S_AddFriend.Parser.ParseFrom(protoBody);
        Log("[MockServer] AddFriend: " + req.MyAccount + " -> " + req.TargetAccount);
        S2C_AddFriendRsp rsp = new S2C_AddFriendRsp();
        if (req.MyAccount == req.TargetAccount)
            rsp.Result = AddFriendResult.AddFriendSelf;
        else if (!_users.ContainsKey(req.TargetAccount))
            rsp.Result = AddFriendResult.AddFriendNotFound;
        else if (_friendLists.TryGetValue(req.MyAccount, out List<string> myFriends) && myFriends.Contains(req.TargetAccount))
            rsp.Result = AddFriendResult.AddFriendAlreadyExists;
        else
        {
            if (!_friendLists.ContainsKey(req.MyAccount)) _friendLists[req.MyAccount] = new List<string>();
            _friendLists[req.MyAccount].Add(req.TargetAccount);
            if (!_reverseFriends.ContainsKey(req.TargetAccount)) _reverseFriends[req.TargetAccount] = new List<string>();
            _reverseFriends[req.TargetAccount].Add(req.MyAccount);
            SaveFriendLists();
            rsp.Result = AddFriendResult.AddFriendSuccess;
            UserRecord rec = _users[req.TargetAccount];
            rsp.FriendInfo = new FriendInfo { Uid = rec.Uid, Account = rec.Account, Nickname = rec.Nickname, Level = rec.Level, AvatarId = rec.AvatarId, Online = _onlineUsers.Contains(req.TargetAccount) };
        }
        return BuildResponse(3001, rsp.ToByteArray());
    }

    /// <summary>
    /// 处理获取好友
    /// </summary>
    /// <param name="protoBody"></param>
    /// <returns></returns>
    private byte[] HandleGetFriendList(byte[] protoBody)
    {
        C2S_GetFriendList req = C2S_GetFriendList.Parser.ParseFrom(protoBody);
        Log("[MockServer] GetFriendList: " + req.MyAccount);
        S2C_FriendList rsp = new S2C_FriendList();
        if (_friendLists.TryGetValue(req.MyAccount, out List<string> friendAccounts))
        {
            foreach (string fa in friendAccounts)
            {
                if (_users.TryGetValue(fa, out UserRecord rec))
                    rsp.Friends.Add(new FriendInfo { Uid = rec.Uid, Account = rec.Account, Nickname = rec.Nickname, Level = rec.Level, AvatarId = rec.AvatarId, Online = _onlineUsers.Contains(fa) });
            }
        }
        return BuildResponse(3101, rsp.ToByteArray());
    }

    /// <summary>
    /// 发送好友申请
    /// </summary>
    /// <param name="protoBody"></param>
    /// <returns></returns>
     private byte[] HandleSendFriendRequest(byte[] protoBody)
    {
        C2S_SendFriendRequest req = C2S_SendFriendRequest.Parser.ParseFrom(protoBody);
        Log($"[MockServer] FriendRequest: {req.From} -> {req.To}");

        if(req.From == req.To) return null;
        if(!_users.ContainsKey(req.To)) return null; //是否有改用户
        if(_friendLists.TryGetValue(req.From,out var myList) && myList.Contains(req.To)) return null; //是否已经添加过
        if(!_pendingRequests.ContainsKey(req.To)) _pendingRequests[req.To] = new List<string>();
        if(_pendingRequests[req.To].Contains(req.From)) return null; //已经申请过了

        _pendingRequests[req.To].Add(req.From);

        //如果对方在线，实时推送申请通知
        Log($"[MockServer] NotifyCheck: onlineContains={_onlineUsers.Contains(req.To)}, hasClient={_accountToClient.ContainsKey(req.To)}");
        if (_onlineUsers.Contains(req.To))
        {
            UserRecord fromUser = _users[req.From];
            S2C_FriendRequestNotify notify = new S2C_FriendRequestNotify
            {
                From = req.From,
                FromNickname = fromUser.Nickname,
                FromLevel = fromUser.Level
            };
            
            if(_accountToClient.TryGetValue(req.To,out TcpClient client))
            {
                try
                {
                    byte[] data = BuildResponse(3301,notify.ToByteArray());
                    client.GetStream().Write(data,0,data.Length);
                    client.GetStream().Flush();
                }
                catch{ }
            }

        }
        return null;
    }
    /// <summary>
    /// 响应好友申请(同意/拒绝)
    /// </summary>
    /// <param name="protoBody"></param>
    /// <returns></returns>
    private byte[] HandleRespondFriendRequest(byte[] protoBody)
    {
        C2S_RespondFriendRequest req = C2S_RespondFriendRequest.Parser.ParseFrom(protoBody);
        Log($"[MockServer] RespondFriend: {req.MyAccount} {(req.Accept ? "accept" : "reject")} {req.FromAccount}");

        S2C_RespondFriendRequestRsp rsp = new S2C_RespondFriendRequestRsp();

        //从待处理列表里移除
        if(_pendingRequests.TryGetValue(req.MyAccount,out var pending))
            pending.Remove(req.FromAccount);

        if (req.Accept)
        {
            //同意
            //双向添加到好友列表
            if(!_friendLists.ContainsKey(req.MyAccount))
                _friendLists[req.MyAccount] = new List<string>();
            if(!_friendLists.ContainsKey(req.FromAccount))
                _friendLists[req.FromAccount] = new List<string>();
            
            _friendLists[req.MyAccount].Add(req.FromAccount);
            _friendLists[req.FromAccount].Add(req.MyAccount);

            //反向映射 双向 更新
            if (!_reverseFriends.ContainsKey(req.FromAccount))
                _reverseFriends[req.FromAccount] = new List<string>();
            if (!_reverseFriends.ContainsKey(req.MyAccount))
                _reverseFriends[req.MyAccount] = new List<string>();

            _reverseFriends[req.FromAccount].Add(req.MyAccount);
            _reverseFriends[req.MyAccount].Add(req.FromAccount);

            SaveFriendLists();

            rsp.Result = FriendRequestResult.FriendRequestAccept;
            UserRecord friend = _users[req.FromAccount];
            rsp.FriendInfo = new FriendInfo
            {
                Uid = friend.Uid,Account = friend.Account,
                Nickname = friend.Nickname,Level = friend.Level,
                AvatarId = friend.AvatarId,
                Online = _onlineUsers.Contains(req.FromAccount)
            };

        }
        else
        {
            //拒绝
            rsp.Result = FriendRequestResult.FriendRequestRejected;
        }

        //给申请人的返回结果(如果在线则推送)
        byte[] responseData = BuildResponse(3401,rsp.ToByteArray());
        if(_accountToClient.TryGetValue(req.FromAccount, out TcpClient fromClient))
        {
            try
            {
                fromClient.GetStream().Write(responseData,0,responseData.Length);
                fromClient.GetStream().Flush();
            }
            catch {}
        }

        return null;
    }

    private byte[] HandleDeleteFriend(byte[] protoBody)
    {
        C2S_DeleteFriend req = C2S_DeleteFriend.Parser.ParseFrom(protoBody);
        S2C_DeleteFriendRsp rsp = new S2C_DeleteFriendRsp();

        //双向从_friendLists移除
        if(_friendLists.TryGetValue(req.MyAccount,out var MyList))
            MyList.Remove(req.FriendAccount);
        if(_friendLists.TryGetValue(req.FriendAccount,out var friendList))
            friendList.Remove(req.MyAccount);

        //双向从_reverseFriends 移除
        if(_reverseFriends.TryGetValue(req.MyAccount,out var myWatchers))
            myWatchers.Remove(req.FriendAccount);
        if(_reverseFriends.TryGetValue(req.FriendAccount,out var friendWatchers))
            friendWatchers.Remove(req.MyAccount);
        
        //持久化
        SaveFriendLists();

        //返回
        rsp.Result = DeleteFriendResult.DeleteFriendSuccess;
        byte[] responseData = BuildResponse(3501, rsp.ToByteArray());

        // 通知被删方（如果在线）
        if (_accountToClient.TryGetValue(req.FriendAccount, out TcpClient friendClient))
        {
            try { friendClient.GetStream().Write(responseData, 0, responseData.Length); friendClient.GetStream().Flush(); } catch { }
        }

        return responseData;
    }
    /// <summary>
    /// 处理发送消息
    /// </summary>
    /// <param name="protoBody"></param>
    /// <returns></returns>
    private byte[] HandleSendChatMsg(byte[] protoBody)
    {
        C2S_SendChatMsg req = C2S_SendChatMsg.Parser.ParseFrom(protoBody);
        Log("[MockServer] Chat: " + req.From + " -> " + req.To + ": " + req.Text);
        string key = req.From.CompareTo(req.To) < 0 ? req.From + "_" + req.To : req.To + "_" + req.From;
        if (!_chatHistory.ContainsKey(key)) _chatHistory[key] = new List<ChatMsg>();
        ChatMsg msg = new ChatMsg { From = req.From, To = req.To, Text = req.Text, Timestamp = req.Timestamp, MsgId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        _chatHistory[key].Add(msg);
        if (_onlineUsers.Contains(req.To))
        {
            S2C_ChatMsg push = new S2C_ChatMsg { From = req.From, To = req.To, Text = req.Text, Timestamp = req.Timestamp };
            if (_accountToClient.TryGetValue(req.To, out TcpClient client))
            {
                try 
                { 
                    byte[] data = BuildResponse(4001, push.ToByteArray()); 
                    client.GetStream().Write(data, 0, data.Length); 
                    client.GetStream().Flush(); 
                } 
                catch { }
            }
        }
        return null;
    }

    /// <summary>
    /// 处理获取历史消息
    /// </summary>
    /// <param name="protoBody"></param>
    /// <returns></returns>
    private byte[] HandleGetChatHistory(byte[] protoBody)
    {
        C2S_GetChatHistory req = C2S_GetChatHistory.Parser.ParseFrom(protoBody);
        string key = req.MyAccount.CompareTo(req.FriendAccount) < 0 ? req.MyAccount + "_" + req.FriendAccount : req.FriendAccount + "_" + req.MyAccount;
        S2C_ChatHistory rsp = new S2C_ChatHistory();
        if (_chatHistory.TryGetValue(key, out List<ChatMsg> list)) rsp.Messages.AddRange(list);
        return BuildResponse(4101, rsp.ToByteArray());
    }

    /// <summary>
    /// 处理心跳消息
    /// </summary>
    /// <param name="protoBody"></param>
    /// <returns></returns>
    private byte[] HandleHeartbeat(byte[] protoBody)
    {
        C2S_Heartbeat req = C2S_Heartbeat.Parser.ParseFrom(protoBody);
        return BuildResponse(1001, new S2C_Heartbeat { ServerTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() }.ToByteArray());
    }
    

    private void LoadFriendLists()
    {
        try
        {
            if (!File.Exists(_friendListFilePath)) return;
            string json = File.ReadAllText(_friendListFilePath);
            Dictionary<string, object> root = MiniJSON.Json.Deserialize(json) as Dictionary<string, object>;
            if (root == null) return;
            if (root.TryGetValue("friendLists", out object obj) && obj is List<object> list)
            {
                foreach (object item in list)
                {
                    Dictionary<string, object> entry = item as Dictionary<string, object>;
                    if (entry == null) continue;
                    string account = entry["account"].ToString();
                    List<string> friends = new List<string>();
                    if (entry.TryGetValue("friends", out object fObj) && fObj is List<object> fList)
                        foreach (object f in fList) friends.Add(f.ToString());
                    _friendLists[account] = friends;
                }
            }
            Log("[MockServer] Loaded " + _friendLists.Count + " friend lists");
            RebuildReverseFriends();
        }
        catch (Exception ex) { Log("[MockServer] LoadFriendLists fail: " + ex.Message); }
    }

    private void RebuildReverseFriends()
    {
        _reverseFriends.Clear();
        foreach (var kv in _friendLists)
            foreach (string friend in kv.Value)
            {
                if (!_reverseFriends.ContainsKey(friend)) _reverseFriends[friend] = new List<string>();
                _reverseFriends[friend].Add(kv.Key);
            }
    }

    private void SaveFriendLists()
    {
        try
        {
            List<object> list = new List<object>();
            foreach (var kv in _friendLists)
            {
                List<object> fList = new List<object>();
                foreach (string f in kv.Value) fList.Add(f);
                list.Add(new Dictionary<string, object> { { "account", kv.Key }, { "friends", fList } });
            }
            string json = MiniJSON.Json.Serialize(new Dictionary<string, object> { { "friendLists", list } });
            File.WriteAllText(_friendListFilePath, json);
        }
        catch (Exception ex) { Log("[MockServer] SaveFriendLists fail: " + ex.Message); }
    }

    private void NotifyFriendStatus(string account, bool online)
    {
        if (!_reverseFriends.TryGetValue(account, out List<string> watchers)) return;
        foreach (string watcher in watchers)
        {
            if (!_onlineUsers.Contains(watcher)) continue;
            if (!_accountToClient.TryGetValue(watcher, out TcpClient client)) continue;
            try
            {
                S2C_FriendStatusNotify notify = new S2C_FriendStatusNotify { Account = account, Online = online };
                byte[] data = BuildResponse(MessageId.S2C_FriendStatusNotify, notify.ToByteArray());
                client.GetStream().Write(data, 0, data.Length);
                client.GetStream().Flush();
            }
            catch { }
        }
    }

    private byte[] BuildResponse(int msgId, byte[] protoBody)
    {
        int bodyLen = 4 + protoBody.Length;
        byte[] frame = new byte[4 + bodyLen];
        byte[] lenBytes = BitConverter.GetBytes(bodyLen);
        byte[] idBytes = BitConverter.GetBytes(msgId);
        Buffer.BlockCopy(lenBytes, 0, frame, 0, 4);
        Buffer.BlockCopy(idBytes, 0, frame, 4, 4);
        Buffer.BlockCopy(protoBody, 0, frame, 8, protoBody.Length);
        return frame;
    }

    private void Log(string msg)
    {
        lock (_debugLogs)
        {
            _debugLogs.Enqueue(msg);
            while (_debugLogs.Count > MAX_LOGS) _debugLogs.Dequeue();
        }
    }

    private void Update()
    {
        lock (_debugLogs)
        {
            while (_debugLogs.Count > 0) Debug.Log(_debugLogs.Dequeue());
        }
    }

    private void OnDestroy()
    {
        _isRunning = false;
        try { _listener?.Stop(); } catch { }
        lock (_lock)
        {
            foreach (var c in _clients) { try { c.Close(); } catch { } }
            _clients.Clear();
        }
        _instance = null;
    }
}