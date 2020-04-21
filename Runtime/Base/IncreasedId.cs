namespace Com.Eyu.UnitySocketLibrary
{
    public abstract class IncreasedId
    {
        public readonly long Id;
        private readonly long _idGenerator;

        protected IncreasedId()
        {
            Id = ++_idGenerator;
        }
    }
}