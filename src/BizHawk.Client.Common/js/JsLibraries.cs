using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

using BizHawk.Common;
using BizHawk.Common.StringExtensions;
using BizHawk.Emulation.Common;

namespace BizHawk.Client.Common
{
	public class JsLibraries : IJsLibraries, IDisposable
	{
		private static bool? _isAvailable;
		private static string _availabilityError;

		public static bool IsAvailable
		{
			get
			{
				if (_isAvailable.HasValue) return _isAvailable.Value;
				_isAvailable = TryProbeV8(out _availabilityError);
				return _isAvailable.Value;
			}
		}

		public static string AvailabilityError => _availabilityError;

		private static bool TryProbeV8(out string error)
		{
			error = null;
			try
			{
				using var engine = new V8ScriptEngine(V8ScriptEngineFlags.DisableGlobalMembers);
				var result = engine.Evaluate("1+1");
				if (Convert.ToInt32(result) != 2)
				{
					error = "V8 evaluate returned unexpected result";
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				error = ex.ToString();
				return false;
			}
		}

		public JsLibraries(
			List<JsFile> scriptList,
			IEmulatorServiceProvider serviceProvider,
			IMainFormForApi mainForm,
			Config config,
			Action<string> printCallback,
			IDialogParent dialogParent,
			ApiContainer apiContainer)
		{
			if (!IsAvailable)
			{
				throw new InvalidOperationException($"The V8 JavaScript engine was not able to be loaded: {_availabilityError}");
			}

			_mainForm = mainForm;
			_defaultExceptionCallback = printCallback;
			_logToJsConsoleCallback = o =>
			{
				foreach (object obj in o)
					printCallback(obj?.ToString() ?? "null");
			};
			PathEntries = config.PathEntries;
			ScriptList = scriptList;
			_apiContainer = apiContainer;

			_engine = new V8ScriptEngine(
				V8ScriptEngineFlags.DisableGlobalMembers
				| V8ScriptEngineFlags.EnableTaskPromiseConversion);
			_th = new JsTableHelper(_engine, printCallback);

			ConfigureModuleSearchPath();

			foreach (var lib in ReflectionCache_Biz_Cli_Com.Types
				.Where(static t => typeof(JsLibraryBase).IsAssignableFrom(t) && t.IsSealed))
			{
				if (VersionInfo.DeveloperBuild
					|| lib.GetCustomAttribute<LuaLibraryAttribute>(inherit: false)?.Released is not false)
				{
					if (!ServiceInjector.IsAvailable(serviceProvider, lib))
					{
						Util.DebugWriteLine($"couldn't instantiate {lib.Name}, docs only");
						continue;
					}

					var instance = (JsLibraryBase)Activator.CreateInstance(lib, this, _apiContainer, (Action<string>)(s => Print(s)));
					if (!ServiceInjector.UpdateServices(serviceProvider, instance, mayCache: true))
						throw new Exception("JS lib has required service(s) that can't be fulfilled");

					if (instance is ClientJsLibrary clientLib)
					{
						clientLib.MainForm = _mainForm;
					}
					else if (instance is EventsJsLibrary eventsLib)
					{
						eventsLib.RemoveNamedFunctionMatching = RemoveNamedFunctionMatching;
					}

					AddLibrary(instance);
				}
			}

			ExposeGlobalPrint();
			EmulationJsLibrary.FrameAdvanceCallback = FrameAdvanceAsync;
			EmulationJsLibrary.YieldCallback = EmuYieldAsync;
		}

		private ApiContainer _apiContainer;
		private readonly IMainFormForApi _mainForm;
		private V8ScriptEngine _engine;
		private readonly JsTableHelper _th;
		private readonly Action<string> _defaultExceptionCallback;
		private static Action<object[]> _logToJsConsoleCallback;
		private readonly List<IDisposable> _disposables = new();
		private Thread _currentHostThread;
		private readonly Lock ThreadMutex = new();
		private readonly Stack<JsFile> _runningFiles = new();

		public JsFile CurrentFile
			=> _runningFiles.Count is 0 ? null : _runningFiles.Peek();

		public bool IsRebootingCore { get; set; }

		public bool IsUpdateSupressed { get; set; }

		private readonly IDictionary<Type, JsLibraryBase> Libraries = new Dictionary<Type, JsLibraryBase>();

		public PathEntryCollection PathEntries { get; private set; }

		public ApiGroup ProhibitedApis { get; private set; } = ApiGroup.NONE;

		public List<JsFile> ScriptList { get; }

		public bool FrameAdvanceRequested { get; private set; }

		private EmulationJsLibrary EmulationJsLibrary => (EmulationJsLibrary)Libraries[typeof(EmulationJsLibrary)];

		public JsTableHelper GetTableHelper() => _th;

		public V8ScriptEngine Engine => _engine;

		private void ConfigureModuleSearchPath()
		{
			var jsPath = PathEntries.JsAbsolutePath();
			try { Directory.CreateDirectory(jsPath); }
			catch { /* ignore */ }
			_engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableFileLoading;
			_engine.DocumentSettings.SearchPath = string.Join(";", new[]
			{
				jsPath,
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JS"),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "JS"),
			}.Where(Directory.Exists));
		}

		private void ExposeGlobalPrint()
		{
			_engine.Script.print = new Action<object>(o => Print(o));
		}

		public void Restart(
			IEmulatorServiceProvider newServiceProvider,
			Config config,
			ApiContainer apiContainer)
		{
			_apiContainer = apiContainer;
			PathEntries = config.PathEntries;
			ConfigureModuleSearchPath();
			foreach (var lib in Libraries.Values)
			{
				lib.APIs = _apiContainer;
				if (!ServiceInjector.UpdateServices(newServiceProvider, lib, mayCache: true))
				{
					throw new Exception("JS lib has required service(s) that can't be fulfilled");
				}

				lib.Restarted();
			}
		}

		public void AddLibrary(JsLibraryBase lib)
		{
			if (lib is IRegisterJsFunctions rfLib)
			{
				rfLib.CreateAndRegisterNamedFunction = CreateAndRegisterNamedFunction;
			}

			if (lib is IDisposable disposable)
			{
				_disposables.Add(disposable);
			}

			Libraries[lib.GetType()] = lib;
			// DirectAccess preserves Task→Promise conversion for frameadvance/yield.
			// Explicit lowercase aliases match Lua method names from [LuaMethod].
			var hostName = $"__js_{lib.Name}";
			_engine.AddHostObject(hostName, HostItemFlags.DirectAccess, lib);
			var sb = new System.Text.StringBuilder();
			sb.Append("var ").Append(lib.Name).Append(" = {");
			var first = true;
			foreach (var method in lib.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
			{
				var attr = method.GetCustomAttribute<LuaMethodAttribute>(inherit: false);
				if (attr is null) continue;
				if (!first) sb.Append(',');
				first = false;
				sb.Append(attr.Name).Append(": function() { return ")
					.Append(hostName).Append('.').Append(method.Name)
					.Append("(...arguments); }");
			}
			sb.Append("};");
			_engine.Execute(sb.ToString());
		}

		public void CallSaveStateEvent(string name)
		{
			foreach (var file in ScriptList)
			{
				foreach (var func in file.Functions.Where(static l => l.Event == NamedJsFunction.EVENT_TYPE_SAVESTATE).ToList())
				{
					func.Call(name);
				}
			}
		}

		public void CallLoadStateEvent(string name)
		{
			foreach (var file in ScriptList)
			{
				foreach (var func in file.Functions.Where(static l => l.Event == NamedJsFunction.EVENT_TYPE_LOADSTATE).ToList())
				{
					func.Call(name);
				}
			}
		}

		public void CallFrameBeforeEvent()
		{
			if (IsUpdateSupressed) return;

			foreach (var file in ScriptList)
			{
				foreach (var func in file.Functions.Where(static l => l.Event == NamedJsFunction.EVENT_TYPE_PREFRAME).ToList())
				{
					func.Call();
				}
			}
		}

		public void CallFrameAfterEvent()
		{
			if (IsUpdateSupressed) return;

			foreach (var file in ScriptList)
			{
				foreach (var func in file.Functions.Where(static l => l.Event == NamedJsFunction.EVENT_TYPE_POSTFRAME).ToList())
				{
					func.Call();
				}
			}
		}

		public void Close()
		{
			foreach (var file in ScriptList)
			{
				foreach (var closeCallback in file.Functions
					.Where(static l => l.Event == NamedJsFunction.EVENT_TYPE_CONSOLECLOSE)
					.ToList())
				{
					closeCallback.Call();
				}

				file.Functions.Clear();
			}

			ScriptList.Clear();
			foreach (var disposable in _disposables)
			{
				disposable.Dispose();
			}
			_disposables.Clear();
			_engine?.Dispose();
			_engine = null;
		}

		public void Dispose() => Close();

		private INamedJsFunction CreateAndRegisterNamedFunction(
			ScriptObject function,
			string theEvent,
			ApiGroup prohibitedApis,
			string name = null)
		{
			var file = RequireCurrentFile("event registration");
			var nlf = new NamedJsFunction(function, theEvent, _defaultExceptionCallback, file, this, prohibitedApis, name);
			file.Functions.Add(nlf);
			return nlf;
		}

		private bool RemoveNamedFunctionMatching(Func<INamedJsFunction, bool> predicate)
		{
			var file = RequireCurrentFile("event removal");
			if (file.Functions.FirstOrDefault(predicate) is not NamedJsFunction nlf) return false;
			file.Functions.Remove(nlf);
			return true;
		}

		public void Sandbox(JsFile jsFile, Action callback, Action<string> exceptionCallback = null, ApiGroup prohibitedApis = ApiGroup.NONE)
		{
			var previousApiGroup = ProhibitedApis;
			ProhibitedApis |= prohibitedApis;

			var setThread = SetCurrentThread();
			_runningFiles.Push(jsFile);
			JsSandbox.Sandbox(
				Path.GetDirectoryName(jsFile.Path),
				callback,
				exceptionCallback ?? _defaultExceptionCallback);
			_runningFiles.Pop();
			if (setThread) ClearCurrentThread();

			ProhibitedApis = previousApiGroup;
		}

		public void StartScript(JsFile file)
		{
			if (!IsAvailable) throw new InvalidOperationException("V8 is not available");

			var code = LoadScriptSource(file.Path, out var loadError);
			if (code is null)
			{
				_defaultExceptionCallback(loadError);
				return;
			}

			file.Start();
			file.ScriptTask = null;
			file.YieldCompletion = null;

			Sandbox(file, () =>
			{
				// Wrap user script so top-level await works and errors surface.
				var wrapped = $"(async () => {{\n{code}\n}})()";
				var result = _engine.Evaluate(wrapped);
				if (result is Task task)
				{
					file.ScriptTask = task;
					_ = task.ContinueWith(t =>
					{
						if (t.IsFaulted)
						{
							_defaultExceptionCallback(t.Exception?.GetBaseException().ToString() ?? "JS script faulted");
						}
					}, TaskScheduler.Default);
				}
			});
		}

		private string LoadScriptSource(string path, out string error)
		{
			error = null;
			try
			{
				var text = File.ReadAllText(path);
				if (path.EndsWithOrdinal(".ts"))
				{
					TypeScriptTranspiler.Configure(Path.Combine(PathEntries.JsAbsolutePath(), "vendor", "typescript.js"));
					var (ok, js, transpileError) = TypeScriptTranspiler.Transpile(text, Path.GetFileName(path));
					if (!ok)
					{
						error = transpileError ?? "TypeScript transpile failed";
						return null;
					}
					if (!string.IsNullOrEmpty(transpileError))
					{
						_defaultExceptionCallback(transpileError);
					}
					return js;
				}
				return text;
			}
			catch (Exception ex)
			{
				error = ex.ToString();
				return null;
			}
		}

		public bool ResumeScripts(bool includeFrameWaiters)
		{
			if (ScriptList.Count == 0 || IsUpdateSupressed)
			{
				return false;
			}

			var anyStopped = false;
			foreach (var lf in ScriptList.Where(static lf => lf.State is JsFile.RunState.Running))
			{
				var prohibit = lf.FrameWaiting && !includeFrameWaiters;
				if (prohibit) continue;

				var hadException = false;
				var waitForFrame = false;
				var shouldStop = true;

				if (!lf.RunningEventsOnly)
				{
					if (lf.YieldCompletion != null && includeFrameWaiters)
					{
						var tcs = lf.YieldCompletion;
						lf.YieldCompletion = null;
						waitForFrame = false;
						lf.FrameWaiting = false;
						FrameAdvanceRequested = false;
						// ClearScript runs promise continuations synchronously when the
						// TaskCompletionSource completes. Resume inside Sandbox so
						// CurrentFile is set for the next await emu.frameadvance()/yield().
						Sandbox(lf, () => tcs.TrySetResult(null));
						shouldStop = lf.ScriptTask?.IsCompleted == true && !lf.ShouldKeepRunning();
					}
					else if (lf.ScriptTask is null)
					{
						shouldStop = !lf.ShouldKeepRunning();
					}
					else if (lf.ScriptTask.IsCompleted)
					{
						if (lf.ScriptTask.IsFaulted)
						{
							hadException = true;
							_defaultExceptionCallback(lf.ScriptTask.Exception?.GetBaseException().ToString() ?? "JS error");
						}
						shouldStop = true;
					}
					else if (lf.FrameWaiting)
					{
						waitForFrame = true;
						shouldStop = false;
					}
					else
					{
						shouldStop = false;
					}
				}
				else
				{
					shouldStop = !lf.ShouldKeepRunning();
				}

				if (hadException) shouldStop = true;
				else shouldStop = shouldStop && !lf.ShouldKeepRunning();

				if (shouldStop)
				{
					anyStopped = true;
					lf.Stop();
				}
				else
				{
					lf.FrameWaiting = waitForFrame || lf.YieldCompletion != null;
					lf.RunningEventsOnly = lf.ScriptTask?.IsCompleted == true;
				}
			}

			return anyStopped;
		}

		public object ExecuteString(string command)
		{
			try
			{
				return _engine.Evaluate(command);
			}
			catch (Exception ex)
			{
				_defaultExceptionCallback(ex.ToString());
				return null;
			}
		}

		private void ClearCurrentThread()
		{
			lock (ThreadMutex)
			{
				_currentHostThread = null;
			}
		}

		private bool SetCurrentThread()
		{
			lock (ThreadMutex)
			{
				var wasNull = _currentHostThread is null;
				if (!wasNull && _currentHostThread != Thread.CurrentThread)
				{
					throw new InvalidOperationException("Can't run JS from two host threads at the same time!");
				}
				_currentHostThread = Thread.CurrentThread;
				return wasNull;
			}
		}

		public static void Print(params object[] outputs)
		{
			_logToJsConsoleCallback(outputs);
		}

		private JsFile RequireCurrentFile(string apiName)
		{
			if (CurrentFile is { } current) return current;

			// Fallback if a promise continuation somehow runs outside Sandbox:
			// prefer the single in-flight script that is cooperatively yielding.
			var candidates = ScriptList
				.Where(static f => f.State is JsFile.RunState.Running
					&& f.ScriptTask is { IsCompleted: false })
				.ToList();
			if (candidates.Count == 1) return candidates[0];

			throw new InvalidOperationException(
				$"{apiName} requires an active script context (CurrentFile was null)");
		}

		private Task FrameAdvanceAsync()
		{
			if (ProhibitedApis.HasFlag(ApiGroup.YIELDING))
			{
				throw new InvalidOperationException("emu.frameadvance() is not allowed during any callbacks");
			}

			FrameAdvanceRequested = true;
			var file = RequireCurrentFile("emu.frameadvance()");
			var tcs = new TaskCompletionSource<object>();
			file.YieldCompletion = tcs;
			file.YieldIsFrameAdvance = true;
			file.FrameWaiting = true;
			return tcs.Task;
		}

		private Task EmuYieldAsync()
		{
			if (ProhibitedApis.HasFlag(ApiGroup.YIELDING))
			{
				throw new InvalidOperationException("emu.yield() is not allowed during any callbacks");
			}

			var file = RequireCurrentFile("emu.yield()");
			var tcs = new TaskCompletionSource<object>();
			file.YieldCompletion = tcs;
			file.YieldIsFrameAdvance = false;
			file.FrameWaiting = true;
			return tcs.Task;
		}
	}
}
