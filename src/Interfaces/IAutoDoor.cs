using IZU.Entities;

namespace IZU.Interfaces
{
    public interface IAutoDoor : IDevice, ICanStart, ICanStop, ICanOpen, ICanClose, IEmergency, IReset, IInitial, ISwitch
    {
        Task<string> CloseManualAsync1();
        Task<string> OpenManualAsync1();

    }
}
