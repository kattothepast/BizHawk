using System.Collections.Generic;
using System.Linq;

namespace BizHawk.Client.Common
{
	public static class JsHelper
	{
		public static object EnumerateToJsTable<T>(this JsTableHelper tableHelper, IEnumerable<T> list, int indexFrom = 0)
			=> tableHelper.ListToTable(list as IReadOnlyList<T> ?? list.ToList(), indexFrom);
	}
}
