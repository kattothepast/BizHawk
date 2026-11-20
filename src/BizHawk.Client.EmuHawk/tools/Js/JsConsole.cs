using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk.Properties;
using BizHawk.Emulation.Common;
using BizHawk.WinForms.Controls;

namespace BizHawk.Client.EmuHawk
{
	/// <summary>
	/// Script console for .js / .ts (ClearScript V8). Parallel to <see cref="LuaConsole"/>; does not replace it.
	/// </summary>
	public class JsConsole : ToolFormBase, IToolFormAutoConfig
	{
		public static Icon ToolIcon => Resources.TextDocIcon;

		[RequiredService]
		private IEmulator Emulator { get; set; }

		internal IMainFormForApi MainFormForApi { get; set; }

		private JsLibraries _jsImp;
		private ConsoleJsLibrary _consoleLib;
		private JsFileList _openedFiles;
		private readonly ListBox _scriptList = new() { Dock = DockStyle.Fill };
		private readonly TextBox _output = new()
		{
			Dock = DockStyle.Fill,
			Multiline = true,
			ReadOnly = true,
			ScrollBars = ScrollBars.Both,
			Font = new Font(FontFamily.GenericMonospace, 9f),
		};

		protected override string WindowTitleStatic => "JS Console";

		public JsConsole()
		{
			Icon = ToolIcon;
			Width = 720;
			Height = 480;
			_openedFiles = new([], SessionChangedCallback);

			var menu = new MenuStrip();
			var fileMenu = new ToolStripMenuItemEx { Text = "&File" };
			var openItem = new ToolStripMenuItemEx { Text = "&Open Script...", ShortcutKeys = Keys.Control | Keys.O };
			openItem.Click += (_, _) => OpenScriptDialog();
			var toggleItem = new ToolStripMenuItemEx { Text = "&Toggle Selected", ShortcutKeys = Keys.Control | Keys.T };
			toggleItem.Click += (_, _) => ToggleSelected();
			var removeItem = new ToolStripMenuItemEx { Text = "&Remove Selected", ShortcutKeys = Keys.Delete };
			removeItem.Click += (_, _) => RemoveSelected();
			var stopItem = new ToolStripMenuItemEx { Text = "Stop &All" };
			stopItem.Click += (_, _) => StopAll();
			fileMenu.DropDownItems.Add(openItem);
			fileMenu.DropDownItems.Add(toggleItem);
			fileMenu.DropDownItems.Add(removeItem);
			fileMenu.DropDownItems.Add(stopItem);
			menu.Items.Add(fileMenu);
			MainMenuStrip = menu;
			Controls.Add(menu);

			var split = new SplitContainer
			{
				Dock = DockStyle.Fill,
				Orientation = Orientation.Horizontal,
				SplitterDistance = 160,
			};
			split.Panel1.Controls.Add(_scriptList);
			split.Panel2.Controls.Add(_output);
			Controls.Add(split);
			Controls.SetChildIndex(split, 0);

			Closing += (_, e) =>
			{
				if (!AskSaveChanges())
				{
					e.Cancel = true;
					return;
				}
				_jsImp?.Close();
			};
		}

		private void SessionChangedCallback() => UpdateList();

		public override void Restart()
		{
			if (!JsLibraries.IsAvailable)
			{
				WriteToOutputWindow($"V8 unavailable: {JsLibraries.AvailabilityError}\n");
				return;
			}

			var running = _jsImp?.ScriptList.Where(static f => f.Enabled).Select(static f => f.Path).ToList() ?? new List<string>();

			var apiContainer = ApiManager.RestartJs(
				Emulator.ServiceProvider,
				WriteToOutputWindow,
				MainFormForApi,
				DisplayManager,
				InputManager,
				MovieSession,
				Tools,
				Config,
				Emulator,
				Game,
				DialogController);

			_openedFiles = new(_openedFiles.Where(static f => !f.IsSeparator).ToList(), SessionChangedCallback);
			_jsImp?.Close();
			_jsImp = new JsLibraries(
				_openedFiles.Where(static f => !f.IsSeparator).ToList(),
				Emulator.ServiceProvider,
				MainFormForApi,
				Config,
				WriteToOutputWindow,
				(IDialogParent)MainForm,
				apiContainer);

			_consoleLib = new ConsoleJsLibrary(_jsImp, apiContainer, WriteToOutputWindow) { Tools = Tools };
			_jsImp.AddLibrary(_consoleLib);

			// EmuHawk-only libraries
			TryAddEmuHawkLibrary(typeof(FormsJsLibrary), apiContainer);
			TryAddEmuHawkLibrary(typeof(GuiJsLibrary), apiContainer);
			TryAddEmuHawkLibrary(typeof(TAStudioJsLibrary), apiContainer);

			foreach (var path in running)
			{
				LoadJsFile(path);
			}
		}

		private void TryAddEmuHawkLibrary(Type libType, ApiContainer apiContainer)
		{
			try
			{
				if (!ServiceInjector.IsAvailable(Emulator.ServiceProvider, libType)) return;
				var instance = (JsLibraryBase)Activator.CreateInstance(libType, _jsImp, apiContainer, (Action<string>)WriteToOutputWindow);
				if (!ServiceInjector.UpdateServices(Emulator.ServiceProvider, instance, mayCache: true)) return;
				if (instance is TAStudioJsLibrary tas) { /* Tools injected via service if needed */ }
				_jsImp.AddLibrary(instance);
			}
			catch (Exception ex)
			{
				WriteToOutputWindow($"Failed to add {libType.Name}: {ex.Message}\n");
			}
		}

		public void ResumeScripts(bool includeFrameWaiters)
		{
			if (_jsImp is null) return;
			if (MainForm.IsTurboing && !Config.RunLuaDuringTurbo) return;
			_jsImp.ResumeScripts(includeFrameWaiters);
			UpdateList();
		}

		public void CallStateSaveCallbacks(string name) => _jsImp?.CallSaveStateEvent(name);

		public void CallStateLoadCallbacks(string name) => _jsImp?.CallLoadStateEvent(name);

		protected override void UpdateBefore()
		{
			if (_jsImp is null || _jsImp.IsUpdateSupressed) return;
			_jsImp.CallFrameBeforeEvent();
		}

		protected override void UpdateAfter()
		{
			if (_jsImp is null || _jsImp.IsUpdateSupressed) return;
			_jsImp.CallFrameAfterEvent();
			ResumeScripts(true);
		}

		protected override void FastUpdateBefore()
		{
			if (Config.RunLuaDuringTurbo) UpdateBefore();
		}

		protected override void FastUpdateAfter()
		{
			if (Config.RunLuaDuringTurbo) UpdateAfter();
		}

		public void WriteToOutputWindow(string message)
		{
			if (IsDisposed) return;
			if (InvokeRequired)
			{
				BeginInvoke(() => WriteToOutputWindow(message));
				return;
			}
			_output.AppendText(message.EndsWith("\n", StringComparison.Ordinal) ? message : message + "\n");
		}

		public void ClearOutputWindow()
		{
			if (InvokeRequired)
			{
				BeginInvoke(ClearOutputWindow);
				return;
			}
			_output.Clear();
		}

		public void LoadJsFile(string path)
		{
			if (!JsLibraries.IsAvailable)
			{
				WriteToOutputWindow($"Cannot load script; V8 unavailable: {JsLibraries.AvailabilityError}\n");
				return;
			}

			path = Path.GetFullPath(path);
			if (_openedFiles.Any(f => f.Path == path))
			{
				var existing = _openedFiles.First(f => f.Path == path);
				existing.Stop();
				_jsImp.StartScript(existing);
				UpdateList();
				return;
			}

			var file = new JsFile(path, UpdateList);
			_openedFiles.Add(file);
			if (!_jsImp.ScriptList.Contains(file))
				_jsImp.ScriptList.Add(file);
			_jsImp.StartScript(file);
			UpdateList();
		}

		private void OpenScriptDialog()
		{
			using var ofd = new OpenFileDialog
			{
				Filter = "JS/TS scripts (*.js;*.ts)|*.js;*.ts|All files (*.*)|*.*",
				InitialDirectory = Config.PathEntries.JsAbsolutePath(),
			};
			if (ofd.ShowDialog() == DialogResult.OK)
			{
				LoadJsFile(ofd.FileName);
			}
		}

		private void ToggleSelected()
		{
			if (_scriptList.SelectedIndex < 0 || _scriptList.SelectedIndex >= _openedFiles.Count) return;
			var file = _openedFiles[_scriptList.SelectedIndex];
			if (file.IsSeparator) return;
			if (file.Enabled) file.Stop();
			else _jsImp.StartScript(file);
			UpdateList();
		}

		private void RemoveSelected()
		{
			if (_scriptList.SelectedIndex < 0 || _scriptList.SelectedIndex >= _openedFiles.Count) return;
			var file = _openedFiles[_scriptList.SelectedIndex];
			file.Stop();
			_openedFiles.Remove(file);
			_jsImp.ScriptList.Remove(file);
			UpdateList();
		}

		private void StopAll()
		{
			foreach (var f in _openedFiles.ToList()) f.Stop();
			UpdateList();
		}

		private void UpdateList()
		{
			_scriptList.Items.Clear();
			foreach (var f in _openedFiles)
			{
				if (f.IsSeparator) _scriptList.Items.Add("---");
				else _scriptList.Items.Add($"{(f.Enabled ? "[on]" : "[off]")} {Path.GetFileName(f.Path)}");
			}
		}

		public override bool AskSaveChanges() => true;
	}
}
