namespace BizHawk.Client.Common
{
	public interface IJsLibraries
	{
		JsFile CurrentFile { get; }

		bool IsRebootingCore { get; set; }

		bool IsUpdateSupressed { get; set; }

		PathEntryCollection PathEntries { get; }

		ApiGroup ProhibitedApis { get; }

		JsTableHelper GetTableHelper();

		void Sandbox(JsFile jsFile, Action callback, Action<string> exceptionCallback = null, ApiGroup prohibitedApis = ApiGroup.NONE);
	}
}
