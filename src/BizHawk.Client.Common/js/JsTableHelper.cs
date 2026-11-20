using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;

using Microsoft.ClearScript;

namespace BizHawk.Client.Common
{
	/// <summary>
	/// Builds plain JS objects/arrays for script return values (curated surface; no unrestricted CLR dump).
	/// </summary>
	public sealed class JsTableHelper
	{
		private readonly Action<string> _logCallback;

		private readonly ScriptEngine _engine;

		public JsTableHelper(ScriptEngine engine, Action<string> logCallback)
		{
			_engine = engine;
			_logCallback = logCallback;
		}

		public ScriptObject CreateTable()
			=> (ScriptObject)_engine.Evaluate("({})");

		public object DictToTable<T>(IReadOnlyDictionary<string, T> dictionary)
		{
			var table = CreateTable();
			foreach (var (k, v) in dictionary) table[k] = v;
			return table;
		}

		public IReadOnlyList<object> EnumerateValues(object table)
		{
			if (table is null) return Array.Empty<object>();
			if (table is IList list)
			{
				var fromList = new object[list.Count];
				for (var i = 0; i < list.Count; i++) fromList[i] = list[i];
				return fromList;
			}

			if (table is ScriptObject so)
			{
				var values = new List<object>();
				var names = new HashSet<string>(so.PropertyNames);
				// Prefer dense array indices 0..n-1 (JS), fall back to Lua-style 1..n
				if (names.Contains("length") && so["length"] is IConvertible lenConv)
				{
					var length = lenConv.ToInt32(CultureInfo.InvariantCulture);
					for (var i = 0; i < length; i++) values.Add(so[i]);
					return values;
				}

				for (var i = 1; ; i++)
				{
					var key = i.ToString(CultureInfo.InvariantCulture);
					if (!names.Contains(key) && !so.PropertyIndices.Any(idx => idx == i)) break;
					values.Add(names.Contains(key) ? so[key] : so[i]);
				}

				return values;
			}

			if (table is IDictionary dict)
			{
				var values = new List<object>();
				for (var i = 1; ; i++)
				{
					if (!dict.Contains(i) && !dict.Contains((long)i) && !dict.Contains(i.ToString(CultureInfo.InvariantCulture))) break;
					values.Add(dict.Contains(i) ? dict[i] : dict.Contains((long)i) ? dict[(long)i] : dict[i.ToString(CultureInfo.InvariantCulture)]);
				}
				return values;
			}

			_logCallback("EnumerateValues: unsupported table type");
			return Array.Empty<object>();
		}

		public IReadOnlyList<T> EnumerateValues<T>(object table)
			=> EnumerateValues(table).Cast<T>().ToList();

		public object ListToTable<T>(IReadOnlyList<T> list, int indexFrom = 0)
		{
			if (indexFrom == 0)
			{
				var arr = _engine.Evaluate($"new Array({list.Count})");
				var so = (ScriptObject)arr;
				for (var i = 0; i < list.Count; i++) so[i.ToString(CultureInfo.InvariantCulture)] = list[i];
				return arr;
			}

			var table = CreateTable();
			for (int i = 0, l = list.Count; i != l; i++) table[(indexFrom + i).ToString(CultureInfo.InvariantCulture)] = list[i];
			return table;
		}

		public object MemoryBlockToTable(IReadOnlyList<byte> bytes, long startAddr)
		{
			var table = CreateTable();
			var length = bytes.Count;
			var iArray = 0;
			var iDict = startAddr;
			while (iArray < length) table[(iDict++).ToString(CultureInfo.InvariantCulture)] = bytes[iArray++];
			return table;
		}

		public object ObjectToTable(object obj)
		{
			var table = CreateTable();
			var so = table;
			foreach (var method in obj.GetType().GetMethods())
			{
				if (!method.IsPublic || method.IsStatic) continue;
				var foundAttrs = method.GetCustomAttributes(typeof(LuaMethodAttribute), false);
				if (foundAttrs.Length == 0) continue;
				var name = ((LuaMethodAttribute)foundAttrs[0]).Name;
				so[name] = new JsBoundMethod(obj, method);
			}
			return table;
		}


		public IEnumerable<KeyValuePair<object, object>> EnumeratePairs(object table)
		{
			if (table is null) yield break;
			if (table is ScriptObject so)
			{
				foreach (var name in so.PropertyNames)
				{
					if (name == "length") continue;
					yield return new KeyValuePair<object, object>(name, so[name]);
				}
				foreach (var index in so.PropertyIndices)
				{
					yield return new KeyValuePair<object, object>((long)index, so[index]);
				}
				yield break;
			}
			if (table is IDictionary dict)
			{
				foreach (DictionaryEntry entry in dict)
				{
					yield return new KeyValuePair<object, object>(entry.Key, entry.Value);
				}
			}
		}

		public Color ParseColor(object o) => ParseColor(o, safe: false, _logCallback) ?? throw new ArgumentException("failed to parse Color", nameof(o));

		public Color? SafeParseColor(object o) => ParseColor(o, safe: true, _logCallback);

		private static Color? ParseColor(object o, bool safe, Action<string> logCallback)
		{
			switch (o)
			{
				case null:
					return null;
				case Color c:
					return c;
				case double d:
					return ParseColor((int)(long)d, safe, logCallback);
				case int i:
					return Color.FromArgb(i);
				case long l:
					return Color.FromArgb((int)l);
				case string s:
					if (s.Length > 0 && s[0] is '#' && (s.Length is 7 or 9))
					{
						var i1 = uint.Parse(s.Substring(1), NumberStyles.HexNumber);
						if (s.Length is 7) i1 |= 0xFF000000U;
						return ParseColor(unchecked((int)i1), safe, logCallback);
					}
					var fromName = Color.FromName(s);
					if (fromName.IsNamedColor) return fromName;
					if (safe) logCallback($"ParseColor: not a known color name (\"{s}\")");
					return null;
				default:
					if (safe) logCallback("ParseColor: coercing object/table to string");
					return ParseColor(o.ToString(), safe, logCallback);
			}
		}

		private sealed class JsBoundMethod
		{
			private readonly object _target;
			private readonly MethodInfo _method;

			public JsBoundMethod(object target, MethodInfo method)
			{
				_target = target;
				_method = method;
			}

			public object Invoke(params object[] args)
				=> _method.Invoke(_target, args);
		}
	}
}
