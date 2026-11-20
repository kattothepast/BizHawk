using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BizHawk.Client.Common
{
	public class JsFile
	{
		public JsFile(string path, Action onFunctionListChange)
		{
			Path = path;
			State = RunState.Disabled;
			Functions = new(onFunctionListChange);
		}

		private JsFile(bool isSeparator) : this("", () => { })
		{
			IsSeparator = isSeparator;
		}

		public static JsFile SeparatorInstance => new(true);

		public string Path { get; }
		public bool Enabled => State != RunState.Disabled;
		public bool Paused => State == RunState.Paused;
		public bool IsSeparator { get; }
		public bool FrameWaiting { get; set; }
		public bool RunningEventsOnly { get; set; }

		/// <summary>Pending async script body (top-level await / frameAdvance continuation).</summary>
		public Task ScriptTask { get; set; }

		public TaskCompletionSource<object> YieldCompletion { get; set; }

		public bool YieldIsFrameAdvance { get; set; }

		public JsFunctionList Functions { get; }

		private readonly List<IDisposable> _disposables = new();

		public enum RunState
		{
			Disabled,
			Running,
			Paused,
			AwaitingStart,
		}

		public RunState State { get; private set; }

		public void AddDisposable(IDisposable disposable) => _disposables.Add(disposable);

		public void RemoveDisposable(IDisposable disposable) => _disposables.Remove(disposable);

		public bool ShouldKeepRunning()
		{
			return _disposables.Exists(static d => d is IKeepFileRunning)
				|| Functions.Any(static f => f.Event != NamedJsFunction.EVENT_TYPE_ENGINESTOP);
		}

		public void Stop()
		{
			if (State == RunState.Disabled && ScriptTask is null)
			{
				return;
			}

			State = RunState.Disabled;

			foreach (var func in Functions
				.Where(static l => l.Event == NamedJsFunction.EVENT_TYPE_ENGINESTOP)
				.ToList())
			{
				func.Call();
			}
			Functions.Clear();

			foreach (var disposable in _disposables.ToList())
				disposable.Dispose();
			_disposables.Clear();

			YieldCompletion?.TrySetCanceled();
			YieldCompletion = null;
			ScriptTask = null;
			FrameWaiting = false;
			RunningEventsOnly = false;
		}

		public void Start()
		{
			State = RunState.Running;
			FrameWaiting = false;
			RunningEventsOnly = false;
		}

		public void ScheduleStart()
		{
			if (State != RunState.Disabled) throw new InvalidOperationException("A JS file that wasn't stopped was scheduled to start.");

			State = RunState.AwaitingStart;
		}

		public void TogglePause()
		{
			if (State == RunState.Paused)
			{
				State = RunState.Running;
			}
			else if (State == RunState.Running)
			{
				State = RunState.Paused;
			}
		}
	}
}
