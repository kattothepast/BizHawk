using Microsoft.ClearScript;

namespace BizHawk.Client.Common
{
	public abstract class JsLibraryBase
	{
		public delegate INamedJsFunction NLFAddCallback(
			ScriptObject function,
			string theEvent,
			ApiGroup prohibitedApis = ApiGroup.YIELDING,
			string name = null);

		public delegate bool NLFRemoveCallback(Func<INamedJsFunction, bool> predicate);

		public PathEntryCollection PathEntries { get; set; }

		protected JsLibraryBase(IJsLibraries jsLibsImpl, ApiContainer apiContainer, Action<string> logOutputCallback)
		{
			LogOutputCallback = logOutputCallback;
			_jsLibsImpl = jsLibsImpl;
			_th = _jsLibsImpl.GetTableHelper();
			APIs = apiContainer;
			PathEntries = _jsLibsImpl.PathEntries;
		}

		public abstract string Name { get; }

		public ApiContainer APIs { get; set; }

		protected readonly Action<string> LogOutputCallback;

		protected readonly IJsLibraries _jsLibsImpl;

		protected readonly JsTableHelper _th;

		public virtual void Restarted() {}

		protected void Log(string message)
			=> LogOutputCallback?.Invoke(message);
	}
}
