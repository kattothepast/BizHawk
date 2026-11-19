using System.Windows.Forms;

namespace BizHawk.Client.EmuHawk
{
	internal class JsButton : Button
	{
		protected override void OnClick(EventArgs e)
		{
			(Parent as JsWinform)?.DoLuaEvent(Handle);
			base.OnClick(e);
		}
	}
}
