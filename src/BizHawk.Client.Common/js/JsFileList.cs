using System.Collections.Generic;
using System.IO;
using System.Text;

using BizHawk.Common.PathExtensions;
using BizHawk.Common.StringExtensions;

namespace BizHawk.Client.Common
{
	public class JsFileList : List<JsFile>
	{
		private bool _changes;

		private readonly Action ChangedCallback;

		public bool Changes
		{
			get => _changes;
			set
			{
				_changes = value;
				if (ChangedCallback != null && _changes != value)
				{
					ChangedCallback();
				}
			}
		}

		public string Filename { get; set; } = "";

		public JsFileList(IReadOnlyCollection<JsFile> collection, Action onChanged)
		{
			ChangedCallback = onChanged;
			if (collection != null) AddRange(collection);
		}

		public void StopAllScripts()
		{
			ForEach(static lf => lf.Stop());
		}

		public new void Clear()
		{
			StopAllScripts();
			Filename = "";
			Changes = false;
			base.Clear();
		}

		public new void Add(JsFile item)
		{
			Changes = true;
			base.Add(item);
		}

		public new void Insert(int index, JsFile item)
		{
			Changes = true;
			base.Insert(index, item);
		}

		public new bool Remove(JsFile item)
		{
			Changes = true;
			return base.Remove(item);
		}

		public bool Load(string path, bool disableOnLoad, Action onFunctionListChange)
		{
			var file = new FileInfo(path);
			if (!file.Exists)
			{
				return false;
			}

			Clear();
			using var sr = file.OpenText();
			string line;
			while ((line = sr.ReadLine()) != null)
			{
				if (line.StartsWithOrdinal("---"))
				{
					Add(JsFile.SeparatorInstance);
				}
				else
				{
					var scriptPath = line.Substring(startIndex: 2);
					if (!Path.IsPathRooted(scriptPath))
					{
						var directory = Path.GetDirectoryName(path);
						scriptPath = Path.GetFullPath(Path.Combine(directory ?? "", scriptPath));
					}

					var lf = new JsFile(scriptPath, onFunctionListChange);
					Add(lf);
					if (!disableOnLoad && line.StartsWith('1'))
						lf.ScheduleStart();
				}
			}

			Filename = path;
			return true;
		}

		public FileWriteResult Save(string path)
		{
			var sb = new StringBuilder();
			var saveDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
			foreach (var file in this)
			{
				if (file.IsSeparator)
				{
					sb.AppendLine("---");
				}
				else
				{
					sb
						.Append(file.Enabled ? "1" : "0")
						.Append(' ')
						.Append(file.Path.MakeRelativeTo(saveDirectory))
						.AppendLine();
				}
			}

			var result = FileWriter.Write(path, fs =>
			{
				using var sw = new StreamWriter(fs);
				sw.Write(sb.ToString());
			});

			if (!result.IsError)
			{
				Filename = path;
				Changes = false;
			}

			return result;
		}
	}
}
