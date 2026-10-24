using System;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmPanel : BasePanel
{
   private Text _messageText;
   private Button _acceptBtn;
   private Button _rejectBtn;

   public string ApplicantAccount; //谁申请的(发给FriendController用)
   public string ApplicantName; //显示用

    public override void OnCreate()
    {
        _messageText = Find<Text>("MessageText");
        _acceptBtn = Find<Button>("AcceptBtn");
        _rejectBtn = Find<Button>("RejectBtn");
    }

    public override void OnShow()
    {
        _acceptBtn.onClick.AddListener(OnAcceptClick);
        _rejectBtn.onClick.AddListener(OnRejectClick);
    }

    public void SetMessage(string msg)
    {
        _messageText.text = msg;
    }

    public override void OnHide()
    {
         _acceptBtn.onClick.RemoveListener(OnAcceptClick);
        _rejectBtn.onClick.RemoveListener(OnRejectClick);
    }

    private void OnRejectClick()
    {
        LuaManager.Instance.CallFunction("Controller.FriendController","RespondFriendRequest",ApplicantAccount,false);
        UIManager.Instance.ClosePanel<ConfirmPanel>();
    }

    private void OnAcceptClick()
    {
        LuaManager.Instance.CallFunction("Controller.FriendController","RespondFriendRequest",ApplicantAccount,true);
        UIManager.Instance.ClosePanel<ConfirmPanel>();
    }
}
