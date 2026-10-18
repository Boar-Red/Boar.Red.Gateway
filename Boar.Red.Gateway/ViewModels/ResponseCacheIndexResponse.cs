using BaoXia.Constants.Models;

namespace Boar.Red.Gateway.ViewModels;

public class ResponseCacheIndexResponse : Response
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public int AllResponseCachesCount { get; set; }

	public int ResponsesCountRemoved { get; set; }

	#endregion
}
