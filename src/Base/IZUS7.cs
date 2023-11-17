using IZU.Service;
using S7.Net;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace IZU.Base
{
	public class IZUS7
	{
		private readonly ILogger _logger;
		Plc plc { get; }
		public IZUS7(ILogger logger, string server)
		{
			_logger = logger;
			plc = new Plc(CpuType.S71500, server, 0, 0);
		}

		public async Task<bool> Init()
		{
			if (plc.IsConnected == false)
			{
				try
				{
					await plc.OpenAsync();
				}
				catch (Exception ex)
				{
					_logger.LogError("SERVER {0} OPEN ERROR: {1}", plc.IP, ex.Message);
					plc.Close();
					return false;
				}
			}
			return true;
		}
		bool Connected()
		{
			if (!plc.IsConnected)
			{
				_logger.LogError($"plc server {plc.IP} not connected");
				return false;
			}
			return true;
		}

		public async Task<int?> ReadInt(string key)
		{
			if (!Connected()) return null;

			object? val = await plc.ReadAsync(key);
			var t = val.GetType();
			if (val == null) return null;
			return ((ushort)val).ConvertToShort();
		}
		public async Task<bool> WriteInt(string key, int val)
		{
			if (!Connected()) return false;
			await plc.WriteAsync(key, val);
			return true;
		}

		public async Task<float?> ReadReal(string key)
		{
			if (!Connected()) return null;

			object? val = await plc.ReadAsync(key);
			if (val == null) return null;
			var t = val.GetType();
			return ((uint)val).ConvertToFloat();
		}
		public async Task<bool> WriteReal(string key, float val)
		{
			if (!Connected()) return false;
			await plc.WriteAsync(key, val.ConvertToUInt());
			return true;
		}

		public async Task<bool?> ReadBool(string key)
		{
			if (!Connected()) return null;

			object? val = await plc.ReadAsync(key);
			if (val == null) return null;
			return (bool)val;
		}
		public async Task<bool> WriteBool(string key, bool val)
		{
			if (!Connected()) return false;
			await plc.WriteAsync(key, val);
			return true;
		}

		public async Task ReadBlock()
		{
			/*
方法：public byte[] ReadBytes(DataType dataType, int db, int startByteAdr, int count)
入参：
	1、DataType数据类型，可选择从DB块或者Memory中读取；
	2、db：1：DataBlock=1,Memory=0；
	3、startByteAdr：起始地址，即DB块的起始偏移量；
	4、count:读取大小，该大小由读取的DB块的最后一个数据的偏移量和大小决定，这里最后一个字节WordVariable偏移量为16，数据类型为word，2个字节，因此此次读取为16+2=18个字节。
出参：Byte[],这里Byte[]的大小必然和count的大小是相同的，
*/
			//读取数据选择从DB块中读取，db设置为1，起始地址为0，读取18个字节
			var bytes =await plc.ReadBytesAsync(S7.Net.DataType.DataBlock, 1, 0, 18);
			//取字节0中的第0位
			var db1Bool1 = bytes[0].SelectBit(0);
			Console.WriteLine("DB1.DBX0.0:" + db1Bool1);
			//取字节0中的第1位
			bool db1Bool2 = bytes[0].SelectBit(1); ;
			Console.WriteLine("DB1.DBX0.1:" + db1Bool2);
			//跳到字节2并连续取两个字节数据
			int IntVariable = S7.Net.Types.Int.FromByteArray(bytes.Skip(2).Take(2).ToArray());
			Console.WriteLine("DB1.DBW2.0:" + IntVariable);
			//...
			double RealVariable = S7.Net.Types.Real.FromByteArray(bytes.Skip(4).Take(4).ToArray());
			Console.WriteLine("DB1.DBD4.0:" + RealVariable);
			//...
			int dIntVariable = S7.Net.Types.DInt.FromByteArray(bytes.Skip(8).Take(4).ToArray());
			Console.WriteLine("DB1.DBD8.0: " + dIntVariable);
			//...
			uint dWordVariable = S7.Net.Types.DWord.FromByteArray(bytes.Skip(12).Take(4).ToArray());
			Console.WriteLine("DB1.DBD12.0: " + Convert.ToString(dWordVariable, 16));
			//...
			ushort wordVariable = S7.Net.Types.Word.FromByteArray(bytes.Skip(16).Take(2).ToArray());
			Console.WriteLine("DB1.DBW16.0: " + Convert.ToString(wordVariable, 16));

		}
	}
}
