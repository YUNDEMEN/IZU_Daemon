namespace IZU.Interfaces
{
    public interface IAnotherDataServer
    {
        void Start();
        void UpdateDoorLock(string name, string oht);
        string? GetDoorLock(string name);
    }

}
