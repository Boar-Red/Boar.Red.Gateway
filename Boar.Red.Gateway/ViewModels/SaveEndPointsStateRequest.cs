using System.Collections.Generic;

namespace Boar.Red.Gateway.ViewModels;

public class SaveEndPointsStateRequest
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public Dictionary<string, ICollection<EndPointStateInfo>>? RouterEndPointStateInfes { get; set; }

	public bool IsValid
	{
		get
		{
			if (this.RouterEndPointStateInfes?.Count > 0)
			{
				return true;
			}
			return false;
		}
	}

	#endregion
}