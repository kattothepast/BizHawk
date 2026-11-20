using System.Globalization;
using System.Text;

using BizHawk.Client.Common;

using Microsoft.ClearScript;

namespace BizHawk.Client.EmuHawk
{
	public sealed class ConsoleJsLibrary : JsLibraryBase, IPrintingLibrary
	{
		public ToolManager Tools { get; set; }

		public ConsoleJsLibrary(IJsLibraries jsLibsImpl, ApiContainer apiContainer, Action<string> logOutputCallback)
			: base(jsLibsImpl, apiContainer, logOutputCallback) {}

		public override string Name => "console";

		[LuaMethodExample("console.clear( );")]
		[LuaMethod("clear", "clears the output box of the JS Console window")]
		public void Clear()
		{
			if (Tools.Has<JsConsole>())
			{
				Tools.JsConsole.ClearOutputWindow();
			}
		}

		[LuaMethodExample("console.log( \"New log.\" );")]
		[LuaMethod("log", "Outputs the given object to the output box on the JS Console dialog")]
		public void Log(params object[] outputs)
		{
			LogWithSeparator("\t", "\n", outputs);
		}

		[LuaMethodExample("console.writeline( \"New log line.\" );")]
		[LuaMethod("writeline", "Outputs the given object to the output box on the JS Console dialog")]
		public void WriteLine(params object[] outputs)
		{
			LogWithSeparator("\n", "\n", outputs);
		}

		[LuaMethodExample("console.write( \"New log message.\" );")]
		[LuaMethod("write", "Outputs the given object to the output box on the JS Console dialog")]
		public void Write(params object[] outputs)
		{
			LogWithSeparator("", "", outputs);
		}

		private void LogWithSeparator(string separator, string terminator, params object[] outputs)
		{
			if (!Tools.Has<JsConsole>())
			{
				return;
			}

			var sb = new StringBuilder();

			void SerializeAndWrite(object output)
				=> sb.Append(output switch
				{
					null => "null",
					IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
					_ => output.ToString(),
				});

			if (outputs == null || outputs.Length == 0 || (outputs.Length == 1 && outputs[0] is null))
			{
				Tools.JsConsole.WriteToOutputWindow($"(no return){terminator}");
				return;
			}

			SerializeAndWrite(outputs[0]);
			for (int outIndex = 1, indexAfterLast = outputs.Length; outIndex != indexAfterLast; outIndex++)
			{
				sb.Append(separator);
				SerializeAndWrite(outputs[outIndex]);
			}

			if (!string.IsNullOrEmpty(terminator))
			{
				sb.Append(terminator);
			}

			Tools.JsConsole.WriteToOutputWindow(sb.ToString());
		}
	}
}
