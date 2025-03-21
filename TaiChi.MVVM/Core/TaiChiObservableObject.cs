using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TaiChi.MVVM.Core
{
    public abstract class TaiChiObservableObject : ObservableObject, IDisposable
    {
        private bool _disposed = false;

        ~TaiChiObservableObject()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="manualDisposing">是否手动释放</param>
        protected virtual void Dispose(bool manualDisposing)
        {
            if (!_disposed)
            {
                if (manualDisposing)
                {
                    // Manual release of managed resources.
                    ReleaseManagedResources();
                }

                // Release unmanaged resources.
                ReleaseUnmanagedResources();
                _disposed = true;
            }
        }

        /// <summary>
        /// 手动释放托管资源（子类重写时必须调用基类方法）
        /// </summary>
        protected virtual void ReleaseManagedResources()
        {
        }

        /// <summary>
        /// 释放非托管资源（子类重写时必须调用基类方法）
        /// </summary>
        protected virtual void ReleaseUnmanagedResources()
        {
        }
    }
}