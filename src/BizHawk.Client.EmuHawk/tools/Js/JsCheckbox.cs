using System.Windows.Forms;

namespace BizHawk.Client.EmuHawk
{
	public class JsCheckbox : CheckBox
	{
		private void DoLuaClick(object sender, EventArgs e)
		{
			var parent = Parent as JsWinform;
			parent?.DoLuaEvent(Handle);
		}

		protected override void OnClick(EventArgs e)
		{
			base.OnClick(e);
			DoLuaClick(this, e);
		}
	}
}
