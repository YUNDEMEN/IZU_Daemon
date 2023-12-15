namespace IZU.Base
{
	public class WonderResponse
	{
		public bool ok { get; set; }
		public string? message { get; set; }
		public object? data { get; set; }
		public WonderResponse()
		{
            ok = false;
            message = string.Empty;
			data = null;
		}

		public static WonderResponse Create(object data)
		{
			return new WonderResponse
			{
                ok = false,
				data = data
			};
		}
		public static WonderResponse Error(string errorMsg)
		{
			return new WonderResponse
			{
                ok = false,
                message = errorMsg
			};
		}
	}
}
