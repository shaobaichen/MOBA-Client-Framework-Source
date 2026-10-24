using UnityEngine;
using UnityEngine.UI;

public class TipPanel : BasePanel
{
    private Text _messageText;
    private Button _okBtn;

    public override void OnCreate()
    {
        _messageText = Find<Text>("MessageText");
        _okBtn = Find<Button>("OkBtn");
    }

    public override void OnShow()
    {
        if (_okBtn != null) _okBtn.onClick.AddListener(OnOkClick);
    }

    public override void OnHide()
    {
        if (_okBtn != null) _okBtn.onClick.RemoveListener(OnOkClick);
    }

    public void SetMessage(string msg)
    {
        if (_messageText != null) _messageText.text = msg;
    }

    private void OnOkClick()
    {
        UIManager.Instance.ClosePanel<TipPanel>();
    }
}
