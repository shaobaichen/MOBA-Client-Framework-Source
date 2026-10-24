using System.Collections.Generic;
using UnityEngine;
using XLua;
using System.IO;

/// <summary>
/// LuaManager - Lua管理器
/// 
/// 职责：
/// 1. 初始化xLua虚拟机
/// 2. 加载Lua脚本
/// 3. 提供C#和Lua之间的交互接口
/// 4. 管理Lua脚本的生命周期
/// 
/// xLua工作流程：
/// C#代码 → LuaManager.LoadLua() → 执行Lua脚本             
/// Lua脚本 → LuaFunction.Call() → 调用C#方法
/// 
/// 为什么用单例？
/// xLua的LuaEnv（Lua虚拟机）全局只能有一个，
/// 多个虚拟机会互相干扰。
/// </summary>
public class LuaManager : MonoBehaviour
{
    private static LuaManager _instance;
    public static LuaManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[LuaManager]");
                _instance = go.AddComponent<LuaManager>();
                DontDestroyOnLoad(go);
                _instance.Init();
            }
            return _instance;
        }
    }

    /// <summary>
    /// xLua虚拟机实例
    /// 整个游戏只有一个LuaEnv
    /// </summary>
    private LuaEnv _luaEnv;

    /// <summary>
    /// 缓存的Lua脚本
    /// Key: 脚本路径（如 "Controller/LoginController"）
    /// Value: Lua脚本内容
    /// </summary>
    private Dictionary<string, byte[]> _luaCache = new Dictionary<string, byte[]>();

    /// <summary>
    /// 已加载的Lua模块
    /// Key: 模块名
    /// Value: LuaTable（Lua中的模块表）
    /// </summary>
    private Dictionary<string, LuaTable> _loadedModules = new Dictionary<string, LuaTable>();

    /// <summary>
    /// 初始化Lua虚拟机
    /// </summary>
    private void Init()
    {
        _luaEnv = new LuaEnv();

        // 注册自定义Loader，告诉xLua从哪里加载.lua文件
        _luaEnv.AddLoader(CustomLoader);

        Debug.Log("[LuaManager] xLua虚拟机初始化完成");
    }

    /// <summary>
    /// 自定义Lua脚本加载器
    /// 
    /// 当Lua代码中 require "xxx" 时，
    /// xLua会调用这个方法查找脚本。
    /// 
    /// 加载优先级：
    /// 1. 先尝试从AssetBundle加载（热更新后的Lua）
    /// 2. 再尝试从Resources加载（内置Lua）
    /// 
    /// 为什么不用默认加载器？
    /// 默认加载器只能加载Resources下的.lua.txt文件，
    /// 我们需要支持从AB加载Lua脚本（为了热更新）。
    /// </summary>
    private byte[] CustomLoader(ref string filePath)
    {
        // 将路径中的.替换为/（Lua的require用点分隔）
        string luaPath = filePath.Replace(".", "/") + ".lua";

        // 1. 先检查缓存
        if (_luaCache.TryGetValue(luaPath, out byte[] cachedData))
        {
            return cachedData;
        }

        //先查presistentDataPath 热更新后下载的Lua
        string updateLuaPath = Application.persistentDataPath + "/AssetBundles/Lua/" + luaPath;
        if (File.Exists(updateLuaPath))
        {
            byte[] data = File.ReadAllBytes(updateLuaPath);
            _luaCache[luaPath] = data;
            return data;
        }
        //再查StreamingAssets 安装包自带的Lua
         string streamLuaPath = Application.streamingAssetsPath + "/AssetBundles/Lua/" + luaPath;
        if (File.Exists(streamLuaPath))
        {
            byte[] data = File.ReadAllBytes(streamLuaPath);
            _luaCache[luaPath] = data;
            return data;
        }

        // AB 中尝试加载（如果有打进 AB 的其他文本资源）
        TextAsset abLua = ResourceManager.Instance.LoadAsset<TextAsset>("lua/" + luaPath);
        if (abLua != null)
        {
            byte[] data = abLua.bytes;
            _luaCache[luaPath] = data;
            return data;
        }

        // 2. 尝试从Resources加载（开发阶段用）
        TextAsset textAsset = Resources.Load<TextAsset>("Lua/" + luaPath);
        if (textAsset != null)
        {
            byte[] data = textAsset.bytes;
            _luaCache[luaPath] = data;
            return data;
        }

        Debug.LogWarning($"[LuaManager] 未找到Lua脚本: {luaPath}");
        return null;
    }

    // ========== 公共方法 ==========

    /// <summary>
    /// 执行Lua脚本
    /// 
    /// 使用示例：
    /// LuaManager.Instance.DoString("print('hello world')");
    /// </summary>
    public object[] DoString(string luaCode, string chunkName = "main")
    {
        return _luaEnv.DoString(luaCode, chunkName);
    }

    /// <summary>
    /// 加载并执行Lua文件
    /// 
    /// 使用示例：
    /// LuaManager.Instance.Require("Controller.LoginController");
    /// </summary>
    /// <param name="moduleName">模块名，如 "Controller.LoginController"</param>
    public LuaTable Require(string moduleName)
    {
        if (_loadedModules.TryGetValue(moduleName, out LuaTable cached))
        {
            return cached;
        }

        // 使用xLua的require加载模块
        // require会返回Lua模块的返回值（通常是一个table）
        object[] results = _luaEnv.DoString($"return require('{moduleName}')");
        if (results != null && results.Length > 0 && results[0] is LuaTable)
        {
            LuaTable table = results[0] as LuaTable;
            _loadedModules[moduleName] = table;
            return table;
        }

        Debug.LogError($"[LuaManager] 加载Lua模块失败: {moduleName}");
        return null;
    }

    /// <summary>
    /// 调用Lua中的函数
    /// 
    /// 使用示例：
    /// LuaManager.Instance.CallFunction("LoginController", "OnLogin", account, password);
    /// </summary>
    /// <param name="moduleName">Lua模块名</param>
    /// <param name="functionName">函数名</param>
    /// <param name="args">参数</param>
    public object[] CallFunction(string moduleName, string functionName, params object[] args)
    {
        LuaTable module = Require(moduleName);
        if (module == null) return null;

        // 从table中获取函数
        LuaFunction func = module.Get<LuaFunction>(functionName);
        if (func == null)
        {
            Debug.LogError($"[LuaManager] 未找到函数: {moduleName}.{functionName}");
            return null;
        }

        // 调用Lua函数
        return func.Call(args);
    }

    /// <summary>
    /// 获取LuaTable中的值
    /// </summary>
    public T GetFromTable<T>(string moduleName, string key)
    {
        LuaTable module = Require(moduleName);
        if (module == null) return default;
        return module.Get<T>(key);
    }

    // ========== 生命周期 ==========

    /// <summary>
    /// 每帧调用Lua的Update
    /// 让Lua脚本也能感知Unity的Update
    /// </summary>
    private void Update()
    {
        // xLua需要每帧调用Tick来释放内存
        _luaEnv?.Tick();
    }

    /// <summary>
    /// 销毁Lua虚拟机
    /// 释放所有Lua资源
    /// </summary>
    private void OnDestroy()
    {
        // 清空缓存
        _luaCache.Clear();
        _loadedModules.Clear();

        // 销毁Lua虚拟机
        if (_luaEnv != null)
        {
            _luaEnv.Dispose();
            _luaEnv = null;
        }

        Debug.Log("[LuaManager] xLua虚拟机已销毁");
    }

    /// <summary>
    /// 全量GC（触发Lua垃圾回收）
    /// </summary>
    public void FullGC()
    {
        _luaEnv?.Tick();
    }
}
