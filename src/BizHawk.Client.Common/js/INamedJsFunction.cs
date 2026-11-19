namespace BizHawk.Client.Common
{
	public interface INamedJsFunction
	{
		Guid Guid { get; }

		string GuidStr { get; }

		string Name { get; }

		string Event { get; }

		Action OnRemove { get; set; }

		object[] Call(params object[] args);
	}
}
