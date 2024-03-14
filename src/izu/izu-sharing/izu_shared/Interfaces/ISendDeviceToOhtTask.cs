namespace IZU.Interfaces
{
    public interface ISendDeviceToOhtTask
    {
        void Add(List<Tasks.OhtInfo> infos);
        void Delete(List<Tasks.OhtInfo> infos);
    }

}
