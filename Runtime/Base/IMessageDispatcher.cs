namespace Com.Eyu.UnitySocketLibrary
{
    public interface IMessageDispatcher
    {
        void Dispatch(NetworkSession session, INetworkMessage message);
    }
}