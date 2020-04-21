namespace Com.Eyu.UnitySocketLibrary
{
    public interface INetworkMessage
    {
        Command Cmd { get; set; }
        int ReqId { get; set; }
    }

    public sealed partial class C2S : INetworkMessage {}
    public sealed partial class S2C : INetworkMessage {}
}