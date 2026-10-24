using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MessageRouter - 消息分发器
/// 
/// 职责:
/// 1. 监听 NetManager.OnRawBytesReceived 获取完整帧
/// 2. 解析帧头4字节 → 消息ID
/// 3. 按消息ID分发给注册的业务回调
/// 4. 提供 Send() 封装帧 → 调 NetManager.Send()
/// 
/// 帧格式(NetManager传过来的body部分):
/// [4字节: 消息ID(int)] [Protobuf序列化的消息体]
/// 
/// 注册回调时传入消息ID和 Protobuf 反序列化后的消息对象
/// </size>
public class MessageRouter : MonoBehaviour
{
    public static MessageRouter Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[MessageRouter]");
                _instance = go.AddComponent<MessageRouter>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    private static MessageRouter _instance;

    /// <summary>
    /// 消息处理器字典
    /// Key: 消息ID
    /// Value: 收到该消息后的回调 (参数: 消息体字节,不含4字节消息ID)
    /// </summary>
    private Dictionary<int, List<Action<byte[]>>> _handlers = new Dictionary<int, List<Action<byte[]>>>();

    private void Start()
    {
        NetManager.Instance.OnRawBytesReceived += OnRawBytesReceived;
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && NetManager.Instance != null)
            NetManager.Instance.OnRawBytesReceived -= OnRawBytesReceived;

        _handlers.Clear();
        _instance = null;
    }

    /// <summary>
    /// 收到完整帧(不含长度头)
    /// body格式: [4字节消息ID][Protobuf消息体]
    /// </summary>
    private void OnRawBytesReceived(byte[] body)
    {
        if (body == null || body.Length < 4) return;

        int messageId = BitConverter.ToInt32(body, 0);

        byte[] msgBody = new byte[body.Length - 4];
        Buffer.BlockCopy(body, 4, msgBody, 0, msgBody.Length);

        // 分发给所有注册的回调
        if (_handlers.TryGetValue(messageId, out List<Action<byte[]>> callbacks))
        {
            for (int i = 0; i < callbacks.Count; i++)
            {
                try
                {
                    callbacks[i]?.Invoke(msgBody);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MessageRouter] 消息ID={messageId} 处理异常: {ex}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[MessageRouter] 未注册的消息ID: {messageId}");
        }
    }

    /// <summary>
    /// 注册消息处理器
    /// </summary>
    /// <param name="messageId">消息ID(协议号)</param>
    /// <param name="handler">回调(参数为Protobuf反序列化后的字节)</param>
    public void Register(int messageId, Action<byte[]> handler)
    {
        if (!_handlers.ContainsKey(messageId))
            _handlers[messageId] = new List<Action<byte[]>>();

        _handlers[messageId].Add(handler);
    }

    /// <summary>
    /// 注销消息处理器
    /// </summary>
    public void Unregister(int messageId, Action<byte[]> handler)
    {
        if (_handlers.TryGetValue(messageId, out List<Action<byte[]>> callbacks))
        {
            callbacks.Remove(handler);
            if (callbacks.Count == 0)
                _handlers.Remove(messageId);
        }
    }

    /// <summary>
    /// 发送消息
    /// 完整帧格式: [4字节 总长度][4字节 消息ID][Protobuf消息体]
    /// 总长度 = 4 + msgBody.Length  (msgId 4字节 + body)
    /// NetManager接收端先读4字节长度头，再读对应长度body
    /// </summary>
    public void Send(int messageId, byte[] msgBody)
    {
        if (msgBody == null) msgBody = new byte[0];

        int bodyLen = 4 + msgBody.Length;
        byte[] frame = new byte[4 + bodyLen];  // 4(长度头) + 4(msgId) + body

        byte[] lenBytes = BitConverter.GetBytes(bodyLen);
        byte[] idBytes = BitConverter.GetBytes(messageId);

        Buffer.BlockCopy(lenBytes, 0, frame, 0, 4);
        Buffer.BlockCopy(idBytes, 0, frame, 4, 4);
        Buffer.BlockCopy(msgBody, 0, frame, 8, msgBody.Length);

        NetManager.Instance.Send(frame);
    }

    /// <summary>
    /// 发送消息(泛型版本，接受IMessage对象)
    /// 需要 Protobuf 生成的支持 IMessage 接口的消息类
    /// </summary>
    public void Send<T>(int messageId, T message) where T : class
    {
        // 先用反射调 Serialize，后续接入 Protobuf 后替换为 ToByteArray()
        var method = typeof(T).GetMethod("ToByteArray");
        byte[] body = null;
        if (method != null)
        {
            body = (byte[])method.Invoke(message, null);
        }
        else
        {
            Debug.LogError($"[MessageRouter] {typeof(T).Name} 没有 ToByteArray 方法(需接入Protobuf)");
        }

        if (body == null) body = new byte[0];
        Send(messageId, body);
    }
}
