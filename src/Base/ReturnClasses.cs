namespace IZU.Base
{
    public record class response_object(object data, bool ok, string message);
    public record Resp(string result, string error);
}
