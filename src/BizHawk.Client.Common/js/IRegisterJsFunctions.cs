namespace BizHawk.Client.Common
{
	public interface IRegisterJsFunctions
	{
		JsLibraryBase.NLFAddCallback CreateAndRegisterNamedFunction { get; set; }
	}
}
