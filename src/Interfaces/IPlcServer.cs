using IZU.Entities;

namespace IZU.Interfaces
{
	public interface IPlcServer
	{
		string? IP { get; }
		string ConnectionStatus { get; }
		void Config(List<VariableEntity> variableEntities);
		Task<string> WriteBool(string address, bool boolValue);
	}
}
