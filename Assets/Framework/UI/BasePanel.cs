using UnityEngine;

/// <summary>
/// BasePanel - 所有UI面板的基类
/// 
/// 职责：
/// 定义面板的统一生命周期，所有面板必须继承此类。
/// 
/// 生命周期顺序：
/// OnCreate() → OnShow() → [OnRefresh()] → OnHide()
/// 
/// OnCreate : 面板创建时调用（仅一次），适合做初始化，如获取组件引用
/// OnShow   : 面板每次显示时调用，适合刷新数据、注册事件
/// OnRefresh: 面板刷新时调用（如从后台切回前台），适合更新数据
/// OnHide   : 面板关闭时调用，适合注销事件、清理资源
/// 
/// 为什么Awake/Start不够用？
/// Awake/Start只在Instantiate时执行一次，
/// 但面板可能被多次Open/Close，每次都需要重置状态。
/// 所以需要自定义生命周期方法。
/// </summary>
public class BasePanel : MonoBehaviour
{
    /// <summary>
    /// 面板创建时调用
    /// 只会在第一次Instantiate时执行一次
    /// 适合：获取UI组件引用（GetComponent）、初始化数据
    /// </summary>
    public virtual void OnCreate() { }

    /// <summary>
    /// 面板每次显示时调用
    /// 每次OpenPanel都会执行
    /// 适合：刷新界面数据、注册按钮点击事件
    /// </summary>
    public virtual void OnShow() { }

    /// <summary>
    /// 面板刷新时调用
    /// 当面板已打开，再次OpenPanel时触发
    /// 适合：更新数据展示
    /// </summary>
    public virtual void OnRefresh() { }

    /// <summary>
    /// 面板隐藏/关闭时调用
    /// 适合：注销事件、清理临时数据
    /// </summary>
    public virtual void OnHide() { }

    /// <summary>
    /// 方便子类快速查找子对象组件
    /// </summary>
    protected T Find<T>(string path) where T : Component
    {
        Transform trans = transform.Find(path);
        if (trans == null)
        {
            Debug.LogWarning($"[BasePanel] 未找到子对象: {path}");
            return null;
        }
        return trans.GetComponent<T>();
    }
}
