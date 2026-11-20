using System.Collections.Concurrent;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Reflection;

using Microsoft.ClearScript;

namespace BizHawk.Client.Common
{
	/// <summary>
	/// Exposes a <see cref="JsLibraryBase"/> to V8 using Lua-style lowercase method names from <see cref="LuaMethodAttribute"/>.
	/// </summary>
	public sealed class JsLibraryProxy : DynamicObject
	{
		private readonly JsLibraryBase _library;
		private readonly ConcurrentDictionary<string, MethodInfo> _methods = new(StringComparer.Ordinal);

		public JsLibraryProxy(JsLibraryBase library)
		{
			_library = library;
			foreach (var method in library.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
			{
				var attr = method.GetCustomAttribute<LuaMethodAttribute>(inherit: false);
				if (attr is null) continue;
				_methods[attr.Name] = method;
			}
		}

		public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
		{
			if (!_methods.TryGetValue(binder.Name, out var method)
				&& !_methods.TryGetValue(binder.Name.ToLowerInvariant(), out method))
			{
				result = null;
				return false;
			}

			result = Invoke(method, args ?? Array.Empty<object>());
			return true;
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			if (_methods.TryGetValue(binder.Name, out var method)
				|| _methods.TryGetValue(binder.Name.ToLowerInvariant(), out method))
			{
				result = new JsMethodInvoker(_library, method);
				return true;
			}

			result = null;
			return false;
		}

		private object Invoke(MethodInfo method, object[] args)
		{
			var parameters = method.GetParameters();
			var invokeArgs = new object[parameters.Length];
			for (var i = 0; i < parameters.Length; i++)
			{
				var param = parameters[i];
				if (i < args.Length && args[i] is not Undefined)
				{
					invokeArgs[i] = Coerce(args[i], param.ParameterType);
				}
				else if (param.HasDefaultValue)
				{
					invokeArgs[i] = param.DefaultValue;
				}
				else if (param.ParameterType.IsArray && i == parameters.Length - 1
					&& param.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0)
				{
					invokeArgs[i] = Array.CreateInstance(param.ParameterType.GetElementType()!, 0);
				}
				else
				{
					invokeArgs[i] = param.ParameterType.IsValueType ? Activator.CreateInstance(param.ParameterType) : null;
				}
			}

			// pack remaining args into params array
			if (parameters.Length > 0
				&& parameters[^1].GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0
				&& args.Length >= parameters.Length - 1)
			{
				var elementType = parameters[^1].ParameterType.GetElementType()!;
				var extra = args.Skip(parameters.Length - 1).Select(a => Coerce(a, elementType)).ToArray();
				var packed = Array.CreateInstance(elementType, extra.Length);
				Array.Copy(extra, packed, extra.Length);
				invokeArgs[^1] = packed;
			}

			return method.Invoke(_library, invokeArgs);
		}

		private static object Coerce(object value, Type targetType)
		{
			if (value is null || value is Undefined || value is VoidResult) return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
			if (targetType.IsInstanceOfType(value)) return value;
			if (targetType == typeof(ScriptObject) && value is ScriptObject) return value;
			if (targetType.IsEnum) return Enum.ToObject(targetType, Convert.ChangeType(value, Enum.GetUnderlyingType(targetType), CultureInfo.InvariantCulture));

			var underlying = Nullable.GetUnderlyingType(targetType);
			if (underlying != null)
			{
				if (value is null) return null;
				return Coerce(value, underlying);
			}

			if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(targetType))
			{
				try { return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture); }
				catch { /* fall through */ }
			}

			if (targetType == typeof(object)) return value;
			return value;
		}

		private sealed class JsMethodInvoker
		{
			private readonly JsLibraryBase _library;
			private readonly MethodInfo _method;
			private readonly JsLibraryProxy _proxy;

			public JsMethodInvoker(JsLibraryBase library, MethodInfo method)
			{
				_library = library;
				_method = method;
				_proxy = new JsLibraryProxy(library);
			}

			public object Invoke(params object[] args)
				=> _proxy.Invoke(_method, args ?? Array.Empty<object>());
		}
	}
}
