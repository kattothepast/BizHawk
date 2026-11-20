using System.IO;
using System.Security.Cryptography;
using System.Text;

using Microsoft.ClearScript.V8;

namespace BizHawk.Client.Common
{
	/// <summary>
	/// Transpiles .ts to JS in-process via TypeScript.js (no system Node/tsc).
	/// </summary>
	public static class TypeScriptTranspiler
	{
		private static readonly Lock Sync = new();
		private static string _typescriptJs;
		private static string _typescriptJsPath;

		public static void Configure(string typescriptJsPath)
		{
			_typescriptJsPath = typescriptJsPath;
		}

		public static (bool Ok, string Js, string Error) Transpile(string tsSource, string fileName)
		{
			try
			{
				EnsureTypescriptLoaded();
				if (_typescriptJs is null)
				{
					return (false, null, "TypeScript.js not found; place it under Assets/JS/vendor/typescript.js");
				}

				var cachePath = GetCachePath(tsSource, fileName);
				if (File.Exists(cachePath))
				{
					return (true, File.ReadAllText(cachePath), null);
				}

				using var engine = new V8ScriptEngine(V8ScriptEngineFlags.DisableGlobalMembers);
				engine.Execute(_typescriptJs);
				engine.Script.__tsSource = tsSource;
				engine.Script.__tsFileName = fileName ?? "script.ts";
				var result = engine.Evaluate(@"
					(function() {
						var r = ts.transpileModule(__tsSource, {
							compilerOptions: {
								module: ts.ModuleKind.CommonJS,
								target: ts.ScriptTarget.ES2020,
								strict: false,
								esModuleInterop: true
							},
							fileName: __tsFileName,
							reportDiagnostics: true
						});
						var diags = (r.diagnostics || []).map(function(d) {
							var msg = ts.flattenDiagnosticMessageText(d.messageText, '\n');
							var line = d.file && d.start != null ? d.file.getLineAndCharacterOfPosition(d.start).line + 1 : '?';
							return (__tsFileName + ':' + line + ': ' + msg);
						});
						return { outputText: r.outputText, diagnostics: diags };
					})()
				");

				dynamic dyn = result;
				var diags = dyn.diagnostics as object;
				var diagList = new StringBuilder();
				if (diags is System.Collections.IEnumerable enumerable)
				{
					foreach (var d in enumerable)
					{
						diagList.AppendLine(d?.ToString());
					}
				}

				string output = dyn.outputText as string;
				if (diagList.Length > 0 && string.IsNullOrEmpty(output))
				{
					return (false, null, diagList.ToString());
				}

				Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
				File.WriteAllText(cachePath, output);
				return (true, output, diagList.Length > 0 ? diagList.ToString() : null);
			}
			catch (Exception ex)
			{
				return (false, null, ex.ToString());
			}
		}

		private static void EnsureTypescriptLoaded()
		{
			if (_typescriptJs != null) return;
			lock (Sync)
			{
				if (_typescriptJs != null) return;
				var candidates = new[]
				{
					_typescriptJsPath,
					Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JS", "vendor", "typescript.js"),
					Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "JS", "vendor", "typescript.js"),
					Path.Combine(Environment.CurrentDirectory, "JS", "vendor", "typescript.js"),
					Path.Combine(Environment.CurrentDirectory, "Assets", "JS", "vendor", "typescript.js"),
				};
				foreach (var path in candidates)
				{
					if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
					_typescriptJs = File.ReadAllText(path);
					return;
				}
			}
		}

		private static string GetCachePath(string source, string fileName)
		{
			using var sha = SHA256.Create();
			var hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-", "").ToLowerInvariant();
			var dir = Path.Combine(Path.GetTempPath(), "BizHawkJsTsCache");
			var safeName = string.IsNullOrEmpty(fileName) ? "script" : Path.GetFileNameWithoutExtension(fileName);
			return Path.Combine(dir, $"{safeName}.{hash.Substring(0, 16)}.js");
		}
	}
}
