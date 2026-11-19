using System.Collections;
using System.Collections.Generic;

namespace BizHawk.Client.Common
{
	public class JsFunctionList : IEnumerable<NamedJsFunction>
	{
		private readonly List<NamedJsFunction> _functions = new();

		private readonly Action Changed;

		public int Count
			=> _functions.Count;

		public JsFunctionList(Action onChanged) => Changed = onChanged;

		public NamedJsFunction this[string guid]
			=> Guid.TryParseExact(guid, format: "D", out var parsed)
				? _functions.Find(nlf => nlf.Guid == parsed)
				: null;

		public void Add(NamedJsFunction nlf)
		{
			_functions.Add(nlf);
			Changed();
		}

		public bool Remove(NamedJsFunction function)
		{
			if (!RemoveInner(function)) return false;
			Changed();
			return true;
		}

		private bool RemoveInner(NamedJsFunction function)
		{
			if (!_functions.Remove(function)) return false;
			function.OnRemove?.Invoke();
			return true;
		}

		public void Clear()
		{
			if (Count is 0) return;
			foreach (var function in _functions) function.OnRemove?.Invoke();
			_functions.Clear();
			Changed();
		}

		public IEnumerator<NamedJsFunction> GetEnumerator() => _functions.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => _functions.GetEnumerator();
	}
}
