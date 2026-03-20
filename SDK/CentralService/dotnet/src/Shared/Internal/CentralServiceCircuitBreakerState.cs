using System;

namespace CentralService.Shared.Internal
{
    /// <summary>
    /// 在进程内维护单个中心服务端点的熔断状态。
    /// </summary>
    internal sealed class CentralServiceCircuitBreakerState
    {
        private readonly object _syncRoot = new object();
        private readonly int _failureThreshold;
        private readonly TimeSpan _breakDuration;
        private readonly int _recoveryThreshold;
        private CircuitBreakerMode _mode;
        private int _failureCount;
        private int _halfOpenSuccessCount;
        private DateTimeOffset _openUntilUtc;

        public CentralServiceCircuitBreakerState(int failureThreshold, TimeSpan breakDuration, int recoveryThreshold)
        {
            _failureThreshold = Math.Max(1, failureThreshold);
            _breakDuration = breakDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : breakDuration;
            _recoveryThreshold = Math.Max(1, recoveryThreshold);
            _mode = CircuitBreakerMode.Closed;
        }

        /// <summary>
        /// 判断当前请求是否允许进入目标端点。
        /// </summary>
        public bool TryAllowRequest(DateTimeOffset now, out string? skipReason)
        {
            lock (_syncRoot)
            {
                if (_mode == CircuitBreakerMode.Open)
                {
                    if (now >= _openUntilUtc)
                    {
                        _mode = CircuitBreakerMode.HalfOpen;
                        _failureCount = 0;
                        _halfOpenSuccessCount = 0;
                        skipReason = null;
                        return true;
                    }

                    var remaining = _openUntilUtc - now;
                    var remainingSeconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
                    skipReason = $"熔断开启，剩余约 {remainingSeconds} 秒";
                    return false;
                }

                skipReason = null;
                return true;
            }
        }

        /// <summary>
        /// 在一次传输成功后推进熔断状态。
        /// </summary>
        public void ReportSuccess()
        {
            lock (_syncRoot)
            {
                if (_mode == CircuitBreakerMode.HalfOpen)
                {
                    _halfOpenSuccessCount++;
                    if (_halfOpenSuccessCount >= _recoveryThreshold)
                    {
                        ResetToClosed();
                    }

                    return;
                }

                _failureCount = 0;
            }
        }

        /// <summary>
        /// 在一次传输失败后推进熔断状态。
        /// </summary>
        public void ReportFailure(DateTimeOffset now)
        {
            lock (_syncRoot)
            {
                if (_mode == CircuitBreakerMode.HalfOpen)
                {
                    Open(now);
                    return;
                }

                _failureCount++;
                if (_failureCount >= _failureThreshold)
                {
                    Open(now);
                }
            }
        }

        private void Open(DateTimeOffset now)
        {
            _mode = CircuitBreakerMode.Open;
            _failureCount = 0;
            _halfOpenSuccessCount = 0;
            _openUntilUtc = now.Add(_breakDuration);
        }

        private void ResetToClosed()
        {
            _mode = CircuitBreakerMode.Closed;
            _failureCount = 0;
            _halfOpenSuccessCount = 0;
            _openUntilUtc = DateTimeOffset.MinValue;
        }

        private enum CircuitBreakerMode
        {
            Closed = 0,
            Open = 1,
            HalfOpen = 2,
        }
    }
}
