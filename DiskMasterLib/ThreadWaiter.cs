namespace DiskMasterLib
{
    /// <summary>
    /// ManualResetEvent is opposite of what looks logical so use this class to make intention clear.
    /// </summary>
    internal class ThreadWaiter
    {
        private ManualResetEvent _event;

        public ThreadWaiter(bool beginAslWillWait)
        {
            _event = new(initialState: !beginAslWillWait);
        }

        public void SetWait()
            => _event.Reset();

        public void SetNoWait()
            => _event.Set();

        public bool WouldWait()
            => _event.WaitOne(0) ? false : true;

        public void WaitIfSet()
            => _event.WaitOne();
    }
}
