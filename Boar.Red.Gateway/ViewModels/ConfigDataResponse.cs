using BaoXia.Constants.Models;

namespace Boar.Red.Gateway.ViewModels;

public class ConfigDataResponse<T> : Response
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public T? Data { get; set; }

	#endregion
}
