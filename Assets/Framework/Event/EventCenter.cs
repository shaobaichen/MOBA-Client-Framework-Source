using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EventCenter - 全局事件总线
/// 
/// 职责:
/// 1. 解耦模块间通信, 任何模块都能发事件/订阅事件
/// 2. 以字符串为Key, 支持泛型参数传递
/// 
/// 为什么不用UnityEvent?
/// - UnityEvent 需要挂在GameObject上, 跨模块不够灵活
/// - EventCenter 全局静态, Lua也能通过CS.EventCenter.Instance调用
/// 
/// 使用:
///   EventCenter.Instance.AddListener<string>("OnLogin", OnLoginHandler);
///   EventCenter.Instance.Fire<string>("OnLogin", "token123");
///   EventCenter.Instance.RemoveListener<string>("OnLogin", OnLoginHandler);
/// </summary>
public class EventCenter
{
    private static EventCenter _instance;
    public static EventCenter Instance
    {
        get
        {
            if (_instance == null)
                _instance = new EventCenter();
            return _instance;
        }
    }

    /// <summary>
    /// Key: 事件名
    /// Value: 委托链 (Delegate 是所有委托的基类)
    /// </summary>
    private Dictionary<string, Delegate> _eventTable = new Dictionary<string, Delegate>();

    /// <summary>
    /// 订阅事件
    /// </summary>
    public void AddListener<T>(string eventName, Action<T> callback)
    {
        if (_eventTable.TryGetValue(eventName, out Delegate existing))
        {
            _eventTable[eventName] = Delegate.Combine(existing, callback);
        }
        else
        {
            _eventTable[eventName] = callback;
        }
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    public void RemoveListener<T>(string eventName, Action<T> callback)
    {
        if (_eventTable.TryGetValue(eventName, out Delegate existing))
        {
            Delegate after = Delegate.Remove(existing, callback);
            if (after == null)
                _eventTable.Remove(eventName);
            else
                _eventTable[eventName] = after;
        }
    }

    /// <summary>
    /// 触发事件(无参数)
    /// </summary>
    public void Fire(string eventName)
    {
        if (_eventTable.TryGetValue(eventName, out Delegate d))
        {
            (d as Action)?.Invoke();
        }
    }

    /// <summary>
    /// 触发事件(1个参数)
    /// </summary>
    public void Fire<T>(string eventName, T arg)
    {
        if (_eventTable.TryGetValue(eventName, out Delegate d))
        {
            (d as Action<T>)?.Invoke(arg);
        }
    }

    /// <summary>
    /// 触发事件(2个参数)
    /// </summary>
    public void Fire<T1, T2>(string eventName, T1 arg1, T2 arg2)
    {
        if (_eventTable.TryGetValue(eventName, out Delegate d))
        {
            (d as Action<T1, T2>)?.Invoke(arg1, arg2);
        }
    }

    /// <summary>
    /// 触发事件(Lua调用用, 非泛型重载)
    /// </summary>
    public void FireString(string eventName, string arg) => Fire<string>(eventName, arg);

    public void FireInt(string eventName, int arg) => Fire<int>(eventName, arg);

    public void FireObject(string eventName, object arg) => Fire<object>(eventName, arg);

    /// <summary>
    /// 清空所有事件(切换场景时调用)
    /// </summary>
    public void Clear()
    {
        _eventTable.Clear();
    }

    // ===== 预定义事件名常量 =====
    public const string ON_LOGIN_SUCCESS    = "OnLoginSuccess";
    public const string ON_FRIEND_LIST_UPDATED = "OnFriendListUpdated";
    public const string ON_FRIEND_STATUS_CHANGED = "OnFriendStatusChanged";

    public const string ON_NEW_CHAT_MESSAGE = "OnNewChatMessage";
    public const string ON_CHAT_HISTORY = "OnChatHistory";
    public const string ON_SHOW_TIP = "OnShowTip";
}
