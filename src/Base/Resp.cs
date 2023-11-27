namespace IZU.Base
{
	public class WonderResponse
	{
		public int status { get; set; }
		public string? error { get; set; }
		public object? data { get; set; }
		public WonderResponse()
		{
			status = 0; error = string.Empty; data = null;
		}

		public static WonderResponse Create(object data)
		{
			return new WonderResponse
			{
				status = 0,
				data = data
			};
		}
		public static WonderResponse Error(int state, string errorMsg)
		{
			return new WonderResponse
			{
				status = state,
				error = errorMsg
			};
		}
	}
}
