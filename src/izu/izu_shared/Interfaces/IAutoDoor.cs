namespace IZU.Interfaces
{
    public interface IAutoDoor : IDevice, ICanStart, ICanStop, IOperatable, IEmergency, IReset, IInitial, ISwitch
    {
        Task<string> Enable(bool enabled);
        Task<string> JogSpeed(short speed);
        Task<string> AutoSpeed(short speed);
        Task<string> OpenedPosition(short pos);
        Task<string> ClosedPosition(short pos);
    }
}
