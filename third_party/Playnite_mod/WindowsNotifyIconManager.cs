using System.Drawing;
using System.Windows.Forms;

namespace PlayniteMod
{
    public static class WindowsNotifyIconManager
    {
        public static void Notify(Icon icon, string title, string body, Action? clickAction = null)
        {
            var notifyIcon = new NotifyIcon
            {
                Icon = icon,
                BalloonTipTitle = title,
                BalloonTipText = body,
                Visible = true
            };
            notifyIcon.BalloonTipClicked += (o, ea) => { clickAction?.Invoke(); notifyIcon.Dispose(); };
            notifyIcon.BalloonTipClosed += (o, ea) => { notifyIcon.Dispose(); };
            notifyIcon.ShowBalloonTip(60); // Windows Vista and up timeout is 5sec by default, only Windows Accessibility Settings can override this
        }
    }
}