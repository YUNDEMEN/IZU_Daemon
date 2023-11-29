using IZU.Entities;
using IZU.Interfaces;
using S7.Net;
using S7.Net.Types;
using System.Net;

namespace IZU.Base
{
	/*
	    class DB1_TestClass
		{
			public bool v1 { get; set; } 
			public ushort v2 { get; set; } 
			public float v3 { get; set; } 
		}
		public async Task ReadClass()
		{
			DB1_TestClass? ttttt = await plc.ReadClassAsync<DB1_TestClass>(1, 0);
		} 
	 
	 	public void Getm()
		{
			List<S7.Net.Types.DataItem> items = new List<S7.Net.Types.DataItem>
			{
				S7.Net.Types.DataItem.FromAddress("DB1.DBX0.0"),
				S7.Net.Types.DataItem.FromAddress("DB1.DBW2.0"),
				S7.Net.Types.DataItem.FromAddressAndValue("DB1.DBD4.0","0.0f"),
				new S7.Net.Types.DataItem
				{
					DataType=S7.Net.DataType.DataBlock,
					DB=1,
					Count=1,
					VarType= VarType.Real,
					StartByteAdr=4
				}
			};
			plc.ReadMultipleVars(items);


			byte[] bs = plc.ReadBytes(S7.Net.DataType.DataBlock, 1, 0, 8);
			var db1 = S7.Net.Types.Boolean.GetValue(bs.Take(1).ToArray()[0], 0);
			var db2 = S7.Net.Types.Int.FromByteArray(bs.Skip(2).Take(2).ToArray());
			var db3 = S7.Net.Types.Real.FromByteArray(bs.Skip(4).Take(4).ToArray());
		}
	 
	 
	 
	 */
	public class PlcServer : NLogProvider, IPlcServer
	{
		private readonly S7.Net.Types.DataItem _heartbeat_address;
		private int _heart_beat_interval_millionsec = 100;

		private TaskServiceStatus _serviceStatus;
		//private CancellationTokenSource cancelConnect = new CancellationTokenSource();

		private readonly IPAddress? _serverIP;
		private Plc _server;
		private Task _serverReconnectTask;
		private readonly string _deviceName;
		private List<DataItem> _dataItems;
		private IDictionary<int, VariableEntity> hashes = new Dictionary<int, VariableEntity>();

		public string? IP { get { return _serverIP?.ToString(); } }
		public string ConnectionStatus
		{
			get
			{
				return _serviceStatus switch
				{
					TaskServiceStatus.NotStarted => "not startd",
					TaskServiceStatus.Connecting => "disconnected",
					TaskServiceStatus.Connected => "normal",
					_ => "not startd"
				};
			}
		}

		public PlcServer(string deviceName, string ip, int refreshTimeInterval, string heartbeatAddress)
		{
			_deviceName = deviceName;
			if (IPAddress.TryParse(ip, out _serverIP))
			{
				_server = new Plc(CpuType.S71500, ip, 0, 0);
				_server.ReadTimeout = 3000;
			}
			else
				throw new FormatException($"{_deviceName} server IP address format is Incorrect: {ip}");


			_heartbeat_address = S7.Net.Types.DataItem.FromAddress(heartbeatAddress);
			_heart_beat_interval_millionsec = refreshTimeInterval;
			_dataItems = new();

			_serverReconnectTask = new Task(async () =>
			{
				while (true)
				{
					if (_serviceStatus == TaskServiceStatus.Connecting)
					{
						try
						{
							await _server.OpenAsync();
							_serviceStatus = TaskServiceStatus.Connected;
							LogDebug("{0} server {1} heartbeat detecting status:  normal", _deviceName, _serverIP?.ToString());
							//Console.WriteLine("heartbeat detecting status:  normal");
						}
						catch (Exception ex)
						{
							LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", _deviceName, _serverIP?.ToString(), ex.Message);
							//Console.WriteLine("heartbeat detecting status:  disconnected");
						}
					}
					else if (_serviceStatus == TaskServiceStatus.Connected)
					{
						try
						{
							_ = await _server.ReadAsync(
								_heartbeat_address.DataType,
								_heartbeat_address.DB,
								_heartbeat_address.StartByteAdr,
								_heartbeat_address.VarType,
								_heartbeat_address.Count);
							_ = await _server.ReadMultipleVarsAsync(_dataItems);
							_dataItems.ForEach(t => hashes[t.GetHashCode()].Value = t.Value);
							//LogDebug("{0} server {1} heartbeat detecting status:  normal", _deviceName, _serverIP?.ToString());
						}
						catch (Exception ex)
						{
							_serviceStatus = TaskServiceStatus.Connecting;
							//LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", _deviceName, _serverIP?.ToString(), ex.Message);
						}
					}

					await Task.Delay(TimeSpan.FromMilliseconds(_heart_beat_interval_millionsec));
				}
			}, TaskCreationOptions.LongRunning);
		}


		public void Config(List<VariableEntity> variableEntities)
		{
			if (variableEntities.Count == 0) return;

			_dataItems.Clear();
			foreach (var variable in variableEntities)
			{
				try
				{
					if (variable.Disabled || variable.FunctionType == FunctionTypes.W) continue;

					var fake = S7.Net.Types.DataItem.FromAddress(variable.Address);
					var newItem = new S7.Net.Types.DataItem
					{
						DataType = fake.DataType,
						DB = fake.DB,
						Count = fake.Count,
						VarType = (VarType)variable.VariableType,
						StartByteAdr = fake.StartByteAdr,
						BitAdr = fake.BitAdr
					};
					_dataItems.Add(newItem);
					hashes[newItem.GetHashCode()] = variable;
				}
				catch (Exception ex)
				{
					LogWarn("get address  {0}  {1}  error: {2}", _deviceName, variable.Address, ex.Message);
				}
			}
			if (_heart_beat_interval_millionsec > 20)
			{
				_serviceStatus = TaskServiceStatus.Connecting;
				_serverReconnectTask.Start();
			}
			else
			{
				LogWarn($"heart beat detect time interval is too short, please reconfig it larger than 20 ms");
			}
		}


		public async Task<string> WriteBool(string address, bool boolValue)
		{
			if (!_server.IsConnected && _serviceStatus != TaskServiceStatus.Connected)
			{
				LogWarn($"operation write/bool failed, server: {_serverIP} address: {address} error: server {IP} disconnected!");
				return $"server {IP} disconnected!";
			}
			try
			{
				await _server.WriteAsync(address, boolValue);
				return string.Empty;
			}
			catch (Exception ex)
			{
				string exStr = $"operation write/bool failed, server: {_serverIP} address: {address} error: {ex.Message}";
				LogWarn(exStr);
				return exStr;
			}
		}
		public async Task<string> WriteReal(string address, float realValue)
		{
			if (!_server.IsConnected && _serviceStatus != TaskServiceStatus.Connected)
			{
				LogWarn($"operation write/real failed, server: {_serverIP} address: {address} error: server {IP} disconnected!");
				return $"server {IP} disconnected!";
			}
			try
			{
				await _server.WriteAsync(address, realValue.ConvertToUInt());
				return string.Empty;
			}
			catch (Exception ex)
			{
				string exStr = $"operation write/real failed, server: {_serverIP}  address: {address}  error:{ex.Message}";
				LogWarn(exStr);
				return exStr;
			}
		}
		public async Task<string> WriteInt(string address, int intValue)
		{
			if (!_server.IsConnected && _serviceStatus != TaskServiceStatus.Connected)
			{
				LogWarn($"operation write/int failed, server: {_serverIP} address: {address} error: server {IP} disconnected!");
				return $"server {IP} disconnected!";
			}
			try
			{
				await _server.WriteAsync(address, intValue);
				return string.Empty;
			}
			catch (Exception ex)
			{
				string exStr = $"operation write/int failed, server: {_serverIP}  address: {address}  error:{ex.Message}";
				LogWarn(exStr);
				return exStr;
			}
		}


		private async Task<object?> GetValue(string dataPath)
		{
			if (!_server.IsConnected) return null;//$"server {IP} disconnected!";

			object? result = null;
			var data = S7.Net.Types.DataItem.FromAddress(dataPath);
			switch (data.VarType)
			{
				case VarType.Bit:
					result = await _server.ReadAsync(dataPath);
					break;
				case VarType.Byte:
					result = await _server.ReadAsync(dataPath);
					break;
				case VarType.Word:
					result = await _server.ReadAsync(dataPath);
					break;
				case VarType.DWord:
					result = await _server.ReadAsync(dataPath);
					break;
				case VarType.Int:
					result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.Int, 1);
					break;
				case VarType.DInt:
					result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.DInt, 1);
					break;
				case VarType.Real:
					result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.Real, 1);
					break;
				case VarType.LReal:
					result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.LReal, 1);
					break;
				case VarType.String:
					var val = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr + 1, VarType.Byte, 1);
					if (val != null)
					{
						byte count = (byte)val;
						result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr + 2, VarType.String, count);
					}
					break;
				case VarType.S7String:
					val = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr + 1, VarType.Byte, 1);
					if (val != null)
					{
						byte S7StringCount = (byte)val;
						result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.S7String, S7StringCount);
					}
					break;
				case VarType.S7WString:
					val = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr + 2, VarType.Int, 1);
					if (val != null)
					{
						short S7WStringCount = (short)val;
						result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.S7WString, S7WStringCount);
					}
					break;
				//case VarType.S5Time:
				//	return plc.Read(data.DataType, data.DbNumber, data.StartByte, VarType.S5Time, 1);
				//	break;
				case VarType.Counter:
					break;
				case VarType.DateTime:
					result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.DateTime, 1);
					break;
				case VarType.DateTimeLong:
					result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.DateTimeLong, 1);
					break;
			}
			return result;
		}

		public async Task<T?> GetValue<T>(string dataPath)
		{
			object? result = await GetValue(dataPath);
			if (result != null)
			{
				return (T)result;
			}
			return default;
		}

		public async Task<float?> GetFloat(string dataPath)
		{
			object? value = await GetValue(dataPath);
			if (value != null)
			{
				return ((uint)value).ConvertToFloat();
			}
			return null;
		}

		public async Task<bool?> GetBool(string dataPath)
		{
			object? value = await GetValue(dataPath);
			if (value != null)
			{
				return (bool)value;
			}
			return null;
		}

		public async Task<ushort?> GetUShort(string dataPath)
		{
			object? value = await GetValue(dataPath);
			if (value != null)
			{
				return (ushort)value;
			}
			return null;
		}
	}
}
