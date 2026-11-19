using System.IO;

using BizHawk.Common;

namespace BizHawk.Client.Common
{
	public static class JsSandbox
	{
		public static void Sandbox(string currentDirectory, Action callback, Action<string> exceptionCallback)
		{
			string savedEnvironmentCurrDir = null;
			try
			{
				savedEnvironmentCurrDir = Environment.CurrentDirectory;
				if (currentDirectory != null && Directory.Exists(currentDirectory))
				{
					Environment.CurrentDirectory = currentDirectory;
				}

				EnvironmentSandbox.Sandbox(callback);
			}
			catch (Exception ex)
			{
				var exStr = ex + "\n";
				if (ex.InnerException is not null)
				{
					exStr += ex.InnerException + "\n";
				}

				Console.Write(exStr);
				exceptionCallback.Invoke(exStr);
			}
			finally
			{
				if (savedEnvironmentCurrDir != null)
				{
					try { Environment.CurrentDirectory = savedEnvironmentCurrDir; }
					catch { /* ignore */ }
				}
			}
		}
	}
}
