using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ActivityPanel : BasePanel
{
    public static int CurrentActivityIndex = 0;

    [Header("UI组件")]
    public Image _bgImage;
    public Text _titleText;
    public Text _descText;
    public Button _actionBtn;
    public Text _btnText;

    private Dictionary<string, object> _currentActivity;

    private ResourceHandle<Sprite> _bgSpriteHandle;

    public override void OnCreate()
    {
        _bgImage = Find<Image>("BgImage");
        _titleText = Find<Text>("TitleText");
        _descText = Find<Text>("DescText");
        _actionBtn = Find<Button>("ActionBtn");
        _btnText = Find<Text>("BtnText");
    }

    public override void OnShow()
    {
        LoadActivity(CurrentActivityIndex);
    }
    public override void OnRefresh()
    {
    LoadActivity(CurrentActivityIndex);
    }
    public override void OnHide()
{
    if (_bgSpriteHandle != null)
    {
        _bgSpriteHandle.Release();
        _bgSpriteHandle = null;
    }
}

    private void LoadActivity(int index)
    {
        //利用ConfigManager加载Json配置文件
        Dictionary<string, object> config = ConfigManager.Instance.LoadConfig("activity_config");
        if (config == null) return;

        List<object> activities = ConfigManager.Instance.GetList(config, "activities");
        if (activities == null || index < 0 || index >= activities.Count) return;

        _currentActivity = activities[index] as Dictionary<string, object>;
        if (_currentActivity == null) return;

        string title = ConfigManager.Instance.GetString(_currentActivity, "title", "未知活动");
        string desc = ConfigManager.Instance.GetString(_currentActivity, "desc", "");
        string bgImage = ConfigManager.Instance.GetString(_currentActivity, "bgImage", "");
        string btnText = ConfigManager.Instance.GetString(_currentActivity, "btnText", "进入");
        string btnColor = ConfigManager.Instance.GetString(_currentActivity, "btnColor", "#FFFFFF");

        if (_titleText != null) _titleText.text = title;
        if (_descText != null) _descText.text = desc;
        if (_btnText != null) _btnText.text = btnText;

        if (_bgImage != null && !string.IsNullOrEmpty(bgImage))
{
        if (_bgSpriteHandle != null)
        {
            _bgSpriteHandle.Release();
            _bgSpriteHandle = null;
        }
            _bgSpriteHandle = ResourceManager.Instance.LoadAssetHandle<Sprite>(bgImage);
        if (_bgSpriteHandle != null)
        {
            _bgImage.sprite = _bgSpriteHandle.Asset;
            ResourceManager.Instance.BindHandleToOwner(_bgSpriteHandle, gameObject);
        }
}

        if (_actionBtn != null && ColorUtility.TryParseHtmlString(btnColor, out Color color))
        {
            _actionBtn.image.color = color;
            _actionBtn.onClick.RemoveAllListeners();
            _actionBtn.onClick.AddListener(OnActionClick);
        }
    }

    private void OnActionClick()
    {
        string id = ConfigManager.Instance.GetInt(_currentActivity, "id").ToString();
        Debug.Log($"[ActivityPanel] 点击活动 {id}");
    }
}