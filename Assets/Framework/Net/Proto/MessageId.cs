/// <summary>
/// 消息ID常量定义
/// 与 proto 文件中的消息定义一一对应
/// </summary>
public static class MessageId
{
    // 心跳
    public const int C2S_Heartbeat = 1000;
    public const int S2C_Heartbeat = 1001;

    // 通用
    public const int S2C_ErrorNotify = 1999;

    // 登录
    public const int C2S_LoginReq    = 2000;
    public const int S2C_LoginRsp    = 2001;

    // 注册
    public const int C2S_RegisterReq = 2100;
    public const int S2C_RegisterRsp = 2101;

    // 玩家数据(推送)
    public const int S2C_PlayerData  = 2200;

    // 好友
    public const int C2S_AddFriend    = 3000;
    public const int S2C_AddFriendRsp = 3001;
    public const int C2S_GetFriendList    = 3100;
    public const int S2C_GetFriendListRsp = 3101;

    public const int S2C_FriendStatusNotify = 3200;

    public const int C2S_SendFriendRequest = 3300;
    public const int S2C_FriendRequestNotify = 3301;

    public const int C2S_RespondFriendRequest = 3400;
    public const int S2C_RespondFriendRequestRsp = 3401;

    public const int C2S_DeleteFriend = 3500;
    public const int S2C_DeleteFriendRsp = 3501;



    // 聊天
    public const int C2S_SendChatMsg     = 4000;
    public const int S2C_ChatMsg         = 4001;
    public const int C2S_GetChatHistory  = 4100;
    public const int S2C_GetChatHistoryRsp = 4101;
}
