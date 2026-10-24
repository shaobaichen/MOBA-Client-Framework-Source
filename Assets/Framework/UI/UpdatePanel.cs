using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UpdatePanel - 热更新进度显示面板
/// 
/// 放在Top层，在所有UI之上显示更新进度
/// </summary>
public class UpdatePanel : BasePanel
{
    private Text _statusText;     // 状态文字
    private Slider _progressBar;  // 进度条
    private Text _progressText;   // 进度百分比

    public override void OnCreate()
    {
        _statusText = Find<Text>("StatusText");
        _progressBar = Find<Slider>("ProgressBar");
        _progressText = Find<Text>("ProgressText");
    }

    public override void OnShow()
    {
        // 订阅热更事件
        // 这里直接在Update里轮询状态，简单直观
    }

    private void Update()
    {
        if (VersionManager.Instance == null) return;

        // 实时更新进度显示
        if (_statusText != null)
            _statusText.text = VersionManager.Instance.StatusMessage;

        if (_progressBar != null)
            _progressBar.value = VersionManager.Instance.Progress;

        if (_progressText != null)
            _progressText.text = $"{(VersionManager.Instance.Progress * 100):F0}%";

        // 更新完成，自动关闭
        if (VersionManager.Instance.CurrentState == VersionManager.UpdateState.Complete)
        {
            UIManager.Instance.ClosePanel<UpdatePanel>();
        }
    }
}
