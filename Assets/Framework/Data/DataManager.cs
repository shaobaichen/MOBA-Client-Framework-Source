using Game.Proto;
using UnityEngine;

/// <summary>
/// DataManager - 玩家数据中枢
/// 
/// 职责: 持有当前登录玩家的完整数据, 供所有模块读取
/// Lua访问:
///   local data = CS.DataManager.Instance.PlayerData
///   CS.UnityEngine.Debug.Log(data.Nickname)
/// </summary>
public class DataManager : MonoBehaviour
{
    public static DataManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[DataManager]");
                _instance = go.AddComponent<DataManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    private static DataManager _instance;

    /// <summary>
    /// 当前玩家数据(登录成功后由Lua写入)
    /// </summary>
    public PlayerData PlayerData { get; private set; }
    public string CurrentAccount { get; private set; }

    public bool IsLoggedIn => PlayerData != null;

    public void SetPlayerData(PlayerData data, string account)
    {
        PlayerData = data;
        CurrentAccount = account;
        Debug.Log($"[DataManager] 玩家数据已设置: {data.Nickname} Lv.{data.Level}");
    }

    private void OnDestroy()
    {
        _instance = null;
    }
}
