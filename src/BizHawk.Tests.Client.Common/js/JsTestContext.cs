using System.Collections.Generic;
using System.IO;
using System.Linq;

using BizHawk.Client.Common;
using BizHawk.Emulation.Common;
using BizHawk.Tests.Client.Common.Movie;

namespace BizHawk.Tests.Client.Common.js
{
	internal class JsTestContext
	{
		private static readonly string BASE_SCRIPT_PATH = Path.Combine(Environment.CurrentDirectory, "js/scripts");

		private readonly JsLibraries js;

		public List<string> loggedMessages = new();

		private readonly FakeEmulator emulator = new();

		public bool V8Available => JsLibraries.IsAvailable;

		public JsTestContext()
		{
			if (!JsLibraries.IsAvailable)
			{
				return;
			}

			Action<string> print = loggedMessages.Add;
			FakeMainFormForApi mainApi = new();
			Config config = new();

			ApiContainer apiContainer = ApiManager.RestartJs(
				emulator.ServiceProvider,
				print,
				mainApi,
				null!,
				null!,
				null!,
				null!,
				config,
				emulator,
				new GameInfo(),
				null!);

			js = new(
				new List<JsFile>(),
				emulator.ServiceProvider,
				mainApi,
				config,
				print,
				null!,
				apiContainer
			);
		}

		public void AddScript(string path, bool enable)
		{
			if (js is null) return;
			string absolutePath = Path.GetFullPath(Path.Combine(BASE_SCRIPT_PATH, path));
			JsFile jsFile = new(absolutePath, () => { });
			js.ScriptList.Add(jsFile);
			if (enable)
				js.StartScript(jsFile);
		}

		public void RunYielding()
		{
			js?.ResumeScripts(false);
			System.Threading.Thread.Sleep(50);
			js?.ResumeScripts(false);
		}

		public void RunFrameWaiting()
		{
			Controller c = new(emulator.ControllerDefinition);
			emulator.FrameAdvance(c, true);
			js?.CallFrameAfterEvent();
			js?.ResumeScripts(true);
			System.Threading.Thread.Sleep(20);
		}

		public void AssertLogMatches(params string[] messages)
		{
			Assert.AreEqual(messages.Length, loggedMessages.Count);
			for (int i = 0; i < messages.Length; i++)
			{
				Assert.AreEqual(messages[i], loggedMessages[i]);
			}
		}

		public JsFile.RunState GetScriptState(int id)
		{
			return js.ScriptList[id].State;
		}
	}
}
