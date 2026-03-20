using System.Collections.Concurrent;

namespace CentralService.Services.ServiceCircuiting;

public sealed class ServiceCircuitRuntimeStateStore
{
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, ClientServiceCircuitState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, AccessTicketRecord> _tickets = new(StringComparer.OrdinalIgnoreCase);

    internal bool IsOpen(
        ClientIdentityKey clientKey,
        ServiceInstanceKey serviceKey,
        DateTimeOffset nowUtc)
    {
        if (clientKey.IsEmpty || serviceKey.IsEmpty)
        {
            return false;
        }

        return _states.TryGetValue(BuildStateKey(clientKey, serviceKey), out var state)
               && state.IsOpen(nowUtc);
    }

    internal void ReportSuccess(
        ClientIdentityKey clientKey,
        ServiceInstanceKey serviceKey,
        ServiceCircuitBreakerSettings circuitBreaker)
    {
        if (clientKey.IsEmpty || serviceKey.IsEmpty)
        {
            return;
        }

        var state = GetOrAddState(clientKey, serviceKey, circuitBreaker);
        state.ReportSuccess(circuitBreaker);
    }

    internal bool ReportFailure(
        ClientIdentityKey clientKey,
        ServiceInstanceKey serviceKey,
        ServiceCircuitBreakerSettings circuitBreaker,
        DateTimeOffset nowUtc)
    {
        if (clientKey.IsEmpty || serviceKey.IsEmpty)
        {
            return false;
        }

        var state = GetOrAddState(clientKey, serviceKey, circuitBreaker);
        return state.ReportFailure(circuitBreaker, nowUtc);
    }

    internal string CreateTicket(
        ClientIdentityKey clientKey,
        ServiceInstanceKey serviceKey,
        string serviceId,
        string serviceName,
        DateTimeOffset nowUtc)
    {
        CleanupExpiredTickets(nowUtc);

        var ticket = Guid.NewGuid().ToString("N");
        _tickets[ticket] = new AccessTicketRecord
        {
            Ticket = ticket,
            ClientKey = clientKey,
            ServiceKey = serviceKey,
            ServiceId = serviceId ?? string.Empty,
            ServiceName = serviceName ?? string.Empty,
            CreatedAtUtc = nowUtc,
        };
        return ticket;
    }

    internal bool TryConsumeTicket(
        string? ticket,
        DateTimeOffset nowUtc,
        out AccessTicketRecord? record)
    {
        record = null;
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return false;
        }

        CleanupExpiredTickets(nowUtc);
        if (!_tickets.TryRemove(ticket.Trim(), out var found))
        {
            return false;
        }

        record = found;
        return true;
    }

    internal IReadOnlyList<OpenClientCircuitSnapshot> GetOpenClients(
        ServiceInstanceKey serviceKey,
        DateTimeOffset nowUtc)
    {
        if (serviceKey.IsEmpty)
        {
            return Array.Empty<OpenClientCircuitSnapshot>();
        }

        var result = new List<OpenClientCircuitSnapshot>();
        foreach (var pair in _states)
        {
            if (!TryParseStateKey(pair.Key, out var clientKey, out var stateServiceKey))
            {
                continue;
            }

            if (!string.Equals(stateServiceKey.PersistentKey, serviceKey.PersistentKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var snapshot = pair.Value.ToOpenSnapshot(clientKey, nowUtc);
            if (snapshot != null)
            {
                result.Add(snapshot);
            }
        }

        return result
            .OrderBy(x => x.ClientName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.LocalIp, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal void ClearService(ServiceInstanceKey serviceKey)
    {
        if (serviceKey.IsEmpty)
        {
            return;
        }

        foreach (var key in _states.Keys)
        {
            if (!TryParseStateKey(key, out _, out var stateServiceKey))
            {
                continue;
            }

            if (string.Equals(stateServiceKey.PersistentKey, serviceKey.PersistentKey, StringComparison.OrdinalIgnoreCase))
            {
                _states.TryRemove(key, out _);
            }
        }

        foreach (var pair in _tickets)
        {
            if (string.Equals(pair.Value.ServiceKey.PersistentKey, serviceKey.PersistentKey, StringComparison.OrdinalIgnoreCase))
            {
                _tickets.TryRemove(pair.Key, out _);
            }
        }
    }

    internal void ClearServices(IEnumerable<ServiceInstanceKey> serviceKeys)
    {
        if (serviceKeys == null)
        {
            return;
        }

        foreach (var serviceKey in serviceKeys)
        {
            ClearService(serviceKey);
        }
    }

    private static string BuildStateKey(ClientIdentityKey clientKey, ServiceInstanceKey serviceKey)
    {
        return $"{clientKey.PersistentKey}>>{serviceKey.PersistentKey}";
    }

    private static bool TryParseStateKey(
        string compositeKey,
        out ClientIdentityKey clientKey,
        out ServiceInstanceKey serviceKey)
    {
        clientKey = ClientIdentityKey.Empty;
        serviceKey = ServiceInstanceKey.Empty;

        if (string.IsNullOrWhiteSpace(compositeKey))
        {
            return false;
        }

        var segments = compositeKey.Split(new[] { ">>" }, StringSplitOptions.None);
        if (segments.Length != 2)
        {
            return false;
        }

        var clientParts = segments[0].Split('|');
        var serviceParts = segments[1].Split('|');
        if (clientParts.Length != 4 || serviceParts.Length != 5)
        {
            return false;
        }

        if (!int.TryParse(serviceParts[4], out var port))
        {
            return false;
        }

        clientKey = new ClientIdentityKey(clientParts[0], clientParts[1], clientParts[2], clientParts[3]);
        serviceKey = new ServiceInstanceKey(serviceParts[0], serviceParts[1], serviceParts[2], serviceParts[3], port);
        return true;
    }

    private ClientServiceCircuitState GetOrAddState(
        ClientIdentityKey clientKey,
        ServiceInstanceKey serviceKey,
        ServiceCircuitBreakerSettings circuitBreaker)
    {
        return _states.GetOrAdd(
            BuildStateKey(clientKey, serviceKey),
            _ => new ClientServiceCircuitState(circuitBreaker.Normalize()));
    }

    private void CleanupExpiredTickets(DateTimeOffset nowUtc)
    {
        foreach (var pair in _tickets)
        {
            if (nowUtc - pair.Value.CreatedAtUtc > TicketLifetime)
            {
                _tickets.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed class ClientServiceCircuitState
    {
        private readonly object _sync = new();

        private ServiceCircuitBreakerSettings _settings;
        private CircuitMode _mode;
        private int _consecutiveFailures;
        private int _consecutiveSuccesses;
        private DateTimeOffset? _openedAtUtc;
        private DateTimeOffset? _openUntilUtc;

        public ClientServiceCircuitState(ServiceCircuitBreakerSettings settings)
        {
            _settings = settings.Normalize();
            _mode = CircuitMode.Closed;
        }

        public bool IsOpen(DateTimeOffset nowUtc)
        {
            lock (_sync)
            {
                AdvanceClock(nowUtc);
                return _mode == CircuitMode.Open;
            }
        }

        public void ReportSuccess(ServiceCircuitBreakerSettings settings)
        {
            lock (_sync)
            {
                _settings = settings.Normalize(_settings);
                if (_mode == CircuitMode.HalfOpen)
                {
                    _consecutiveSuccesses++;
                    if (_consecutiveSuccesses >= _settings.RecoveryThreshold)
                    {
                        ResetClosed();
                    }

                    return;
                }

                ResetClosed();
            }
        }

        public bool ReportFailure(ServiceCircuitBreakerSettings settings, DateTimeOffset nowUtc)
        {
            lock (_sync)
            {
                _settings = settings.Normalize(_settings);
                AdvanceClock(nowUtc);

                if (_mode == CircuitMode.HalfOpen)
                {
                    Open(nowUtc);
                    return true;
                }

                _consecutiveSuccesses = 0;
                _consecutiveFailures++;
                if (_consecutiveFailures >= _settings.FailureThreshold)
                {
                    Open(nowUtc);
                    return true;
                }

                return false;
            }
        }

        public OpenClientCircuitSnapshot? ToOpenSnapshot(ClientIdentityKey clientKey, DateTimeOffset nowUtc)
        {
            lock (_sync)
            {
                AdvanceClock(nowUtc);
                if (_mode != CircuitMode.Open || _openedAtUtc == null || _openUntilUtc == null)
                {
                    return null;
                }

                return new OpenClientCircuitSnapshot
                {
                    ClientName = clientKey.ClientName,
                    LocalIp = clientKey.LocalIp,
                    OperatorIp = clientKey.OperatorIp,
                    PublicIp = clientKey.PublicIp,
                    OpenedAtUtc = _openedAtUtc.Value,
                    OpenUntilUtc = _openUntilUtc.Value,
                    ConsecutiveFailures = _consecutiveFailures,
                };
            }
        }

        private void AdvanceClock(DateTimeOffset nowUtc)
        {
            if (_mode == CircuitMode.Open && _openUntilUtc != null && nowUtc >= _openUntilUtc.Value)
            {
                _mode = CircuitMode.HalfOpen;
                _consecutiveFailures = 0;
                _consecutiveSuccesses = 0;
            }
        }

        private void Open(DateTimeOffset nowUtc)
        {
            _mode = CircuitMode.Open;
            _openedAtUtc = nowUtc;
            _openUntilUtc = nowUtc.AddMinutes(_settings.BreakDurationMinutes);
            _consecutiveSuccesses = 0;
        }

        private void ResetClosed()
        {
            _mode = CircuitMode.Closed;
            _consecutiveFailures = 0;
            _consecutiveSuccesses = 0;
            _openedAtUtc = null;
            _openUntilUtc = null;
        }

        private enum CircuitMode
        {
            Closed,
            Open,
            HalfOpen,
        }
    }
}
