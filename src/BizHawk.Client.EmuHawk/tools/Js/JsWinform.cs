using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.ClearScript;
using BizHawk.Client.Common;

namespace BizHawk.Client.EmuHawk
{
	public partial class JsWinform : Form, IKeepFileRunning
	{
		public List<LuaEvent> ControlEvents { get; } = new List<LuaEvent>();

		private readonly JsFile _ownerFile;
		private readonly IJsLibraries _luaImp;
		private readonly ScriptObject/*?*/ _closeCallback;
		private bool _closed = false;

		public bool BlocksInputWhenFocused { get; set; } = true;

		public JsWinform(JsFile ownerFile, IJsLibraries luaLibraries, Action<IntPtr> formsWindowClosedCallback, ScriptObject luaCloseCallback = null)
		{
			_ownerFile = ownerFile;
			_luaImp = luaLibraries;
			_closeCallback = luaCloseCallback;

			InitializeComponent();

			Icon = Properties.Resources.TextDocIcon;
			StartPosition = FormStartPosition.CenterParent;

			_ownerFile.AddDisposable(this);
			Closing += (o, e) =>
			{
				if (_closed) return;
				_closed = true;

				formsWindowClosedCallback(Handle); // This one first. _closeCallback may end up disposing this instance.
				if (_closeCallback != null)
				{
					_luaImp.Sandbox(
						jsFile: _ownerFile,
						callback: () => _ = _closeCallback.Invoke(false),
						prohibitedApis: ApiGroup.YIELDING);
				}
				_ownerFile.RemoveDisposable(this);
			};
		}

		public void DoLuaEvent(IntPtr handle)
		{
			_luaImp.Sandbox(
				jsFile: _ownerFile,
				callback: () =>
				{
					foreach (LuaEvent luaEvent in ControlEvents)
					{
						if (luaEvent.Control == handle)
						{
							luaEvent.Event.Invoke(false);
						}
					}
				},
				prohibitedApis: ApiGroup.YIELDING);
		}

		public class LuaEvent
		{
			public LuaEvent(IntPtr handle, ScriptObject luaFunction)
			{
				Event = luaFunction;
				Control = handle;
			}

			public ScriptObject Event { get; }
			public IntPtr Control { get; }
		}
	}
}
