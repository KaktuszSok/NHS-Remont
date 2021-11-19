using System;
using UnityEngine.Assertions;

namespace NHSRemont.Environment.Fractures.NvBlast.Plugins
{
    public abstract class DisposablePtr : IDisposable
    {
        protected void Initialize(IntPtr ptr)
        {
            Assert.IsTrue(this._ptr == IntPtr.Zero);
            this._ptr = ptr;
        }

        protected void ResetPtr()
        {
            this._ptr = IntPtr.Zero;
        }

        protected abstract void Release();

        public IntPtr ptr
        {
            get { return _ptr; }
        }


        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool bDisposing)
        {
            if (_ptr != IntPtr.Zero)
            {
                Release();
                _ptr = IntPtr.Zero;
            }

            if (bDisposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        ~DisposablePtr()
        {
            Dispose(false);
        }

        private IntPtr _ptr = IntPtr.Zero;
    }
}