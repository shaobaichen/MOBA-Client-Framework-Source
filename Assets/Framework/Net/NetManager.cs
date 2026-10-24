using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public enum ConnectState
{
    Disconnected,
    Connecting,
    Connected
}

public class NetManager : MonoBehaviour
{
    public static NetManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[NetManager]");
                _instance = go.AddComponent<NetManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    private static NetManager _instance;

    private Socket _socket;
    private ConnectState _connectState; //连接状态
    private byte[] _receiveBuffer = new byte[BUFFER_SIZE]; //收信息缓冲
    private byte[] _cacheBuffer = new byte[BUFFER_SIZE]; //处理粘包缓冲
    private int _cacheLength; //缓冲长度
    private Queue<byte[]> _sendQueue = new Queue<byte[]>();
    private Queue<byte[]> _messageQueue = new Queue<byte[]>();
    private object _lock = new object();
    private float _lastReceiveTime; //心跳相关
    private int _reconnectCount;
    private bool _startHeartbeatFlag;

    private string _host;
    private int _port;

    private const float HEARTBEAT_INTERVAL = 5f;
    private const float HEARTBEAT_TIMEOUT = 15f;
    private const int MAX_RECONNECT_COUNT = 5;
    private const int BUFFER_SIZE = 4096;


    public Action<byte[]> OnRawBytesReceived;

    private void Start()
    {
    }

    private void Update()
    {
        if (_startHeartbeatFlag)
        {
            _startHeartbeatFlag = false;
            _lastReceiveTime = Time.time;
            StartHeartbeat();
        }

        ProcessMessageQueue();
        CheckHeartbeat();
    }

    private void OnDestroy()
    {
        CloseSocket();
        _instance = null;
    }

    /// <summary>
    /// 连接
    /// </summary>
    /// <param name="host"></param>
    /// <param name="port"></param>
    public void Connect(string host, int port)
    {
        if (_connectState != ConnectState.Disconnected) return;

        _host = host;
        _port = port;
        _connectState = ConnectState.Connecting;

        _reconnectCount = 0;
        _cacheLength = 0;

        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(host), port);
            _socket.BeginConnect(remoteEP, OnConnectCallback, null);
        }
        catch (Exception ex)
        {
            CloseSocket();
        }
    }

    /// <summary>
    /// 连接回调
    /// </summary>
    /// <param name="ar"></param>
    private void OnConnectCallback(IAsyncResult ar)
    {
        try
        {
            _socket.EndConnect(ar);
            _connectState = ConnectState.Connected;

            _reconnectCount = 0;
            _startHeartbeatFlag = true;

            _socket.BeginReceive(_receiveBuffer, 0, BUFFER_SIZE, SocketFlags.None, OnReceiveCallback, null);
        }
        catch (Exception ex)
        {
            Debug.LogError("[NetManager] 连接失败: " + ex.Message);
            CloseSocket();
        }
    }

    /// <summary>
    /// 断联
    /// </summary>
    public void Disconnect()
    {
        if (_connectState == ConnectState.Disconnected) return;
        CloseSocket();
    }

    /// <summary>
    /// 发送
    /// </summary>
    /// <param name="data"></param>
    public void Send(byte[] data)
    {
        if (_connectState != ConnectState.Connected) return;
        if (data == null || data.Length == 0) return;

        lock (_lock)
        {
            bool needStartSend = (_sendQueue.Count == 0);
            _sendQueue.Enqueue(data);
            if (needStartSend)
            {
                BeginSendFromQueue();
            }
        }
    }

    /// <summary>
    /// 从消息队列中发送消息
    /// </summary>
    private void BeginSendFromQueue()
    {
        byte[] data;
        lock (_lock)
        {
            if (_sendQueue.Count == 0) return;
            data = _sendQueue.Peek();
        }

        try
        {
            _socket.BeginSend(data, 0, data.Length, SocketFlags.None, OnSendCallback, null);
        }
        catch (Exception ex)
        {
            CloseSocket();
        }
    }
    /// <summary>
    /// 发送回调
    /// </summary>
    /// <param name="ar"></param> <summary>
    private void OnSendCallback(IAsyncResult ar)
    {
        try
        {
            _socket.EndSend(ar);

            lock (_lock)
            {
                _sendQueue.Dequeue();
            }

            BeginSendFromQueue();
        }
        catch (Exception ex)
        {
            CloseSocket();
        }
    }


    /// <summary>
    /// 关闭Socket连接
    /// </summary>
    private void CloseSocket()
    {
        lock (_lock)
        {
            try { _socket?.Close(); } catch { }
            _socket = null;
        }
        _cacheLength = 0;
        _connectState = ConnectState.Disconnected;

        lock (_lock)
        {
            _sendQueue?.Clear();
            _messageQueue?.Clear();
        }
    }

    /// <summary>
    /// 收消息回调 （处理分包粘包）
    /// </summary>
    /// <param name="ar"></param>
    private void OnReceiveCallback(IAsyncResult ar)
    {
        try
        {
            int bytesRead = _socket.EndReceive(ar);

            if (bytesRead <= 0)
            {
                CloseSocket();
                return;
            }

            Buffer.BlockCopy(_receiveBuffer, 0, _cacheBuffer, _cacheLength, bytesRead);
            _cacheLength += bytesRead;

            while (_cacheLength >= 4)
            {
                int bodyLen = BitConverter.ToInt32(_cacheBuffer, 0);

                if (bodyLen < 0 || bodyLen > 10 * 1024 * 1024)
                {
                    CloseSocket();
                    return;
                }

                if (_cacheLength < 4 + bodyLen) break;

                if (bodyLen == 0)
                {
                    int leftover = _cacheLength - 4;
                    if (leftover > 0)
                        Buffer.BlockCopy(_cacheBuffer, 4, _cacheBuffer, 0, leftover);
                    _cacheLength = leftover;
                    continue;
                }

                byte[] body = new byte[bodyLen];
                Buffer.BlockCopy(_cacheBuffer, 4, body, 0, bodyLen);

                lock (_lock)
                {
                    _messageQueue.Enqueue(body);
                }

                int remain = _cacheLength - 4 - bodyLen;
                if (remain > 0)
                {
                    Buffer.BlockCopy(_cacheBuffer, 4 + bodyLen, _cacheBuffer, 0, remain);
                }
                _cacheLength = remain;
            }

            _socket.BeginReceive(_receiveBuffer, 0, BUFFER_SIZE, SocketFlags.None, OnReceiveCallback, null);
        }
        catch (ObjectDisposedException)
        {
            Debug.LogError("[NetManager] 接收异常: ObjectDisposedException");
            CloseSocket();
        }
        catch (SocketException ex)
        {
            Debug.LogError($"[NetManager] 接收异常: SocketException - {ex.Message}");
            CloseSocket();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetManager] 接收异常: {ex.GetType().Name} - {ex.Message}");
            CloseSocket();
        }
    }

    /// <summary>
    /// 处理收消息队列
    /// </summary>
    private void ProcessMessageQueue()
    {
        lock (_lock)
        {
            while (_messageQueue.Count > 0)
            {
                _lastReceiveTime = Time.time;
                OnRawBytesReceived?.Invoke(_messageQueue.Dequeue());
            }
        }
    }
    

    /// <summary>
    /// 发送心跳
    /// </summary>
    private void StartHeartbeat()
    {
        StartCoroutine(HeartbeatCoroutine());
    }

    private IEnumerator HeartbeatCoroutine()
    {
        while (_connectState == ConnectState.Connected)
        {
            yield return new WaitForSeconds(HEARTBEAT_INTERVAL);

            if (_connectState != ConnectState.Connected) break;

            // 心跳帧: [4字节长度 = 0][空body] → 表示仅长度头,body为空
            byte[] heartbeatFrame = BitConverter.GetBytes(0);
            Send(heartbeatFrame);
            _lastReceiveTime = Time.time;

            if (_lastReceiveTime + HEARTBEAT_TIMEOUT < Time.time)
            {
                CloseSocket();
                TryReconnect();
                break;
            }
        }
    }

    /// <summary>
    /// 检测心跳
    /// </summary>
    private void CheckHeartbeat()
    {
        if (_connectState != ConnectState.Connected) return;

        if (Time.time - _lastReceiveTime > HEARTBEAT_TIMEOUT)
        {
            CloseSocket();
            TryReconnect();
        }
    }

    /// <summary>
    /// 重连
    /// </summary>
    private void TryReconnect()
    {
        _reconnectCount++;
        if (_reconnectCount > MAX_RECONNECT_COUNT)
        {
            Debug.LogError("[NetManager] 重连失败，已超过最大重连次数");
            return;
        }

        Debug.Log($"[NetManager] 第{_reconnectCount}次重连尝试...");
        StartCoroutine(DelayedReconnect());
    }

    private IEnumerator DelayedReconnect()
    {
        yield return new WaitForSeconds(2f);

        string host = _host;
        int port = _port;
        _connectState = ConnectState.Disconnected;

        if (!string.IsNullOrEmpty(host) && port > 0)
        {
            Connect(host, port);
        }
    }
}
