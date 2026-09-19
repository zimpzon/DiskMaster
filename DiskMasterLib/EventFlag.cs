namespace DiskMasterLib
{
    internal class EventFlag
    {
        private int _flag = 0;

        public bool IsSet()
            => Volatile.Read(ref _flag) == 1;

        public void Set()
            => Volatile.Write(ref _flag, 1);

        public void Clear()
            => Volatile.Write(ref _flag, 0);
    }
}
