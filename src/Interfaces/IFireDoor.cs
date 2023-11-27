using IZU.Entities;

namespace IZU.Interfaces
{
	public interface IFireDoor : IDevice, ICanStart, ICanStop, ICanOpen, ICanClose, IEmergency, IReset
	{

	}
}
