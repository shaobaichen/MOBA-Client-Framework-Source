using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ConfigManager - JSON配置管理器
/// </summary>
public class ConfigManager : MonoBehaviour
{
    private static ConfigManager _instance;
    public static ConfigManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[ConfigManager]");
                _instance = go.AddComponent<ConfigManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    /// <summary>
    /// 配置缓存
    /// Key: 配置文件名（不含扩展名）
    /// Value: 解析后的配置数据
    /// </summary>
    private Dictionary<string, object> _configCache = new Dictionary<string, object>();

    /// <summary>
    /// 加载JSON配置
    /// 
    /// 使用示例：
    /// Dictionary<string, object> activityConfig = ConfigManager.Instance.LoadConfig("activity_config");
    /// </summary>
    /// <param name="configName">配置名（对应Resources/Configs/下的JSON文件）</param>
    /// <returns>解析后的配置数据</returns>
    public Dictionary<string, object> LoadConfig(string configName)
    {
        // 检查缓存
        if (_configCache.TryGetValue(configName, out object cached))
        {
            return cached as Dictionary<string, object>;
        }

        TextAsset textAsset = null;

        // 1. 优先从 AB 加载（热更新路径，之后版本的配置在这里）
        textAsset = ResourceManager.Instance.LoadAsset<TextAsset>(configName);

        // 2. AB 没有，再从 Resources 加载（内置初始配置，作为 Fallback）
        if (textAsset == null)
        {
            textAsset = Resources.Load<TextAsset>("Configs/" + configName);
        }

        if (textAsset == null)
        {
            Debug.LogError($"[ConfigManager] 找不到配置文件: {configName}");
            return null;
        }

        // 解析JSON
        object parsed = MiniJSON.Json.Deserialize(textAsset.text);
        Dictionary<string, object> config = parsed as Dictionary<string, object>;

        if (config != null)
        {
            _configCache[configName] = config;
            Debug.Log($"[ConfigManager] 已加载配置: {configName}");
        }

        return config;
    }

    /// <summary>
    /// 从JSON字符串加载配置（用于热更新下发的配置）
    /// </summary>
    public Dictionary<string, object> LoadConfigFromJson(string jsonStr, string cacheKey)
    {
        object parsed = MiniJSON.Json.Deserialize(jsonStr);
        Dictionary<string, object> config = parsed as Dictionary<string, object>;

        if (config != null)
        {
            _configCache[cacheKey] = config;
        }

        return config;
    }

    /// <summary>
    /// 获取配置中的字符串值
    /// </summary>
    public string GetString(Dictionary<string, object> config, string key, string defaultValue = "")
    {
        if (config != null && config.TryGetValue(key, out object value))
            return value.ToString();
        return defaultValue;
    }

    /// <summary>
    /// 获取配置中的整数值
    /// </summary>
    public int GetInt(Dictionary<string, object> config, string key, int defaultValue = 0)
    {
        if (config != null && config.TryGetValue(key, out object value))
            return int.Parse(value.ToString());
        return defaultValue;
    }

    /// <summary>
    /// 获取配置中的列表
    /// </summary>
    public List<object> GetList(Dictionary<string, object> config, string key)
    {
        if (config != null && config.TryGetValue(key, out object value))
            return value as List<object>;
        return null;
    }

    /// <summary>
    /// 清空配置缓存（热更新后调用）
    /// </summary>
    public void ClearCache()
    {
        _configCache.Clear();
    }
}
