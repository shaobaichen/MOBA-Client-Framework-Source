using Google.Protobuf;

/// <summary>
/// ProtoHelper - Protobuf序列化桥接(供Lua调用)
/// 
/// 为什么需要这个类？
/// ToByteArray() 是 Google.Protobuf.MessageExtensions 的扩展方法，
/// xLua 无法直接调用C#扩展方法，所以必须在C#侧桥接。
/// 
/// Lua调用方式:
///   local bytes = CS.ProtoHelper.Serialize(req)
///   local rsp = CS.ProtoHelper.Deserialize(bytes, typeof(CS.Game.Proto.S2C_LoginRsp))
/// </summary>
public static class ProtoHelper
{
    /// <summary>
    /// 序列化 Protobuf 消息为 byte[]
    /// </summary>
    public static byte[] Serialize(object message)
    {
        if (message is IMessage msg)
            return msg.ToByteArray();

        UnityEngine.Debug.LogError("[ProtoHelper] Serialize失败: 对象不是IMessage类型");
        return null;
    }

    /// <summary>
    /// 反序列化 byte[] 为 Protobuf 消息
    /// 用法: local rsp = CS.ProtoHelper.Deserialize(msgBytes, CS.Game.Proto.S2C_LoginRsp)
    /// </summary>
    public static object Deserialize(byte[] data, System.Type type)
    {
        if (data == null || data.Length == 0) return null;

        var parserProp = type.GetProperty("Parser");
        if (parserProp == null)
        {
            UnityEngine.Debug.LogError("[ProtoHelper] Deserialize失败: 类型没有Parser属性");
            return null;
        }

        var parser = parserProp.GetValue(null);
        var parseMethod = parser.GetType().GetMethod("ParseFrom", new[] { typeof(byte[]) });
        if (parseMethod == null)
        {
            UnityEngine.Debug.LogError("[ProtoHelper] Deserialize失败: Parser没有ParseFrom方法");
            return null;
        }

        return parseMethod.Invoke(parser, new object[] { data });
    }
}
