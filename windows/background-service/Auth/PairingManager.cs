using System.Security.Cryptography;
using LANType.Protocol;
using Microsoft.Extensions.Logging;

namespace LANType.Service.Auth;

public class PairingManager
{
    private readonly ILogger<PairingManager> _logger;
    private readonly object _lock = new();

    private string _currentPin = string.Empty;
    private string? _activeSessionToken;
    private string? _pairedDeviceName;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private int _failedAttempts;
    private DateTimeOffset _lockoutUntil = DateTimeOffset.MinValue;

    public string CurrentPin
    {
        get
        {
            lock (_lock) return _currentPin;
        }
    }

    public string? PairedDeviceName
    {
        get
        {
            lock (_lock) return _pairedDeviceName;
        }
    }

    public PairingManager(ILogger<PairingManager> logger)
    {
        _logger = logger;
        GenerateNewPin();
    }

    public string GenerateNewPin()
    {
        lock (_lock)
        {
            // Cryptographically secure 6-digit PIN
            int pinNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
            _currentPin = pinNumber.ToString();
            _activeSessionToken = null;
            _pairedDeviceName = null;
            _failedAttempts = 0;
            _lockoutUntil = DateTimeOffset.MinValue;

            _logger.LogInformation("Generated new pairing PIN: {PIN}", _currentPin);
            return _currentPin;
        }
    }

    public string GetPairingUri(string hostIp, int port)
    {
        lock (_lock)
        {
            string machineName = Uri.EscapeDataString(Environment.MachineName);
            return $"lantype://pair?host={hostIp}&port={port}&pin={_currentPin}&device={machineName}&v={ProtocolConstants.CurrentProtocolVersion}";
        }
    }

    public (bool Success, string? Token, string? Error) ValidateAndPair(PairRequestMessage request)
    {
        lock (_lock)
        {
            if (DateTimeOffset.UtcNow < _lockoutUntil)
            {
                var remaining = (_lockoutUntil - DateTimeOffset.UtcNow).TotalSeconds;
                return (false, null, $"Too many failed attempts. Try again in {Math.Ceiling(remaining)} seconds.");
            }

            if (string.IsNullOrWhiteSpace(request.PairingPin) || request.PairingPin.Trim() != _currentPin)
            {
                _failedAttempts++;
                _logger.LogWarning("Failed pairing attempt #{Attempt} from device '{Device}'", 
                    _failedAttempts, request.DeviceName);

                if (_failedAttempts >= ProtocolConstants.MaxFailedPairingAttempts)
                {
                    _lockoutUntil = DateTimeOffset.UtcNow.AddMinutes(2);
                    _logger.LogWarning("Max failed pairing attempts reached. Locked out for 2 minutes.");
                    return (false, null, "Too many failed attempts. Device locked out for 2 minutes.");
                }

                return (false, null, "Invalid pairing PIN.");
            }

            // Valid PIN
            _failedAttempts = 0;
            _lockoutUntil = DateTimeOffset.MinValue;

            byte[] tokenBytes = new byte[32];
            RandomNumberGenerator.Fill(tokenBytes);
            _activeSessionToken = Convert.ToHexString(tokenBytes).ToLowerInvariant();
            _pairedDeviceName = string.IsNullOrWhiteSpace(request.DeviceName) ? "Unknown Android Device" : request.DeviceName;
            _tokenExpiry = DateTimeOffset.UtcNow.AddMinutes(ProtocolConstants.SessionTokenExpiryMinutes);

            _logger.LogInformation("Successfully paired with device '{Device}'. Session active.", _pairedDeviceName);
            return (true, _activeSessionToken, null);
        }
    }

    public bool IsSessionValid(string? token)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(_activeSessionToken))
            {
                return false;
            }

            if (DateTimeOffset.UtcNow > _tokenExpiry)
            {
                _logger.LogWarning("Session token expired.");
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(token), 
                System.Text.Encoding.UTF8.GetBytes(_activeSessionToken));
        }
    }

    public void InvalidateSession()
    {
        lock (_lock)
        {
            _activeSessionToken = null;
            _pairedDeviceName = null;
            _logger.LogInformation("Active session invalidated.");
        }
    }
}
