using Microsoft.ClearScript;

namespace BizHawk.Client.Common
{
	public sealed class NamedJsFunction : INamedJsFunction
	{
		public const string EVENT_TYPE_CONSOLECLOSE = "OnConsoleClose";

		public const string EVENT_TYPE_ENGINESTOP = "OnExit";

		public const string EVENT_TYPE_INPUTPOLL = "OnInputPoll";

		public const string EVENT_TYPE_LOADSTATE = "OnSavestateLoad";

		public const string EVENT_TYPE_MEMEXEC = "OnMemoryExecute";

		public const string EVENT_TYPE_MEMEXECANY = "OnMemoryExecuteAny";

		public const string EVENT_TYPE_MEMREAD = "OnMemoryRead";

		public const string EVENT_TYPE_MEMWRITE = "OnMemoryWrite";

		public const string EVENT_TYPE_POSTFRAME = "OnFrameEnd";

		public const string EVENT_TYPE_PREFRAME = "OnFrameStart";

		public const string EVENT_TYPE_SAVESTATE = "OnSavestateSave";

		public const string EVENT_TYPE_FUTURE = "BeforeFutureFrame";

		private readonly ScriptObject _function;

		private readonly IJsLibraries _jsImp;

		private readonly Action<string> _exceptionCallback;

		private readonly ApiGroup _prohibitedApis;

		public Action OnRemove { get; set; }

		public NamedJsFunction(ScriptObject function, string theEvent, Action<string> logCallback, JsFile jsFile,
			IJsLibraries jsLibraries, ApiGroup prohibitedApis, string name = null)
		{
			_function = function;
			_jsImp = jsLibraries;
			_exceptionCallback = logCallback;
			_prohibitedApis = prohibitedApis;
			Name = name ?? "Anonymous";
			Event = theEvent;
			JsFile = jsFile;

#pragma warning disable RS0030 // ensure no collisions
			Guid = Guid.NewGuid();
#pragma warning restore RS0030
		}

		public Guid Guid { get; }

		public string GuidStr
			=> Guid.ToString("D");

		public string Name { get; }

		private JsFile JsFile { get; }

		public string Event { get; }

		public object[] Call(params object[] args)
		{
			object[] ret = null;
			_jsImp.Sandbox(
				jsFile: JsFile,
				callback: () =>
				{
					var result = _function.Invoke(asConstructor: false, args);
					ret = result is null ? Array.Empty<object>() : result is object[] arr ? arr : new[] { result };
				},
				exceptionCallback: s => _exceptionCallback($"error running function attached by the event {Event}\nError message: {s}"),
				prohibitedApis: _prohibitedApis);

			return ret;
		}
	}
}
