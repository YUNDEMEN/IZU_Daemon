namespace IZU.Interfaces
{
    public interface ISendDeviceToOhtTask
    {
        void Add(Tasks.OhtInfo oht);
        void Delete(Tasks.OhtInfo oht);
        void Add(List<Tasks.OhtInfo> infos);
        void Delete(List<Tasks.OhtInfo> infos);
    }

}
