using System;

namespace cratesmith.assetui
{
	[AttributeUsage(validOn:AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
	public class DontExpandSOAttribute : Attribute
	{
		
	}
}