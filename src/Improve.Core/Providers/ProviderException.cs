using System.Globalization;
using System.Net;
using Improve.Core.Resources;

namespace Improve.Core.Providers;

public enum ProviderErrorKind
{
    InvalidKey,
    MissingKey,
    RateLimited,
    Unavailable,
    NoNetwork,
    BadRequest,
    Unknown,
}

/// <summary>Falha do provedor. <see cref="Exception.Message"/> é a mensagem pronta para a UI, em pt-BR.</summary>
public sealed class ProviderException : Exception
{
    public ProviderException(ProviderErrorKind kind, string providerName, int? statusCode = null, string? detail = null, Exception? inner = null)
        : base(BuildMessage(kind, providerName, detail), inner)
    {
        Kind = kind;
        StatusCode = statusCode;
    }

    public ProviderErrorKind Kind { get; }
    public int? StatusCode { get; }

    public static ProviderErrorKind KindFromStatus(HttpStatusCode status) => (int)status switch
    {
        401 or 403 => ProviderErrorKind.InvalidKey,
        429 => ProviderErrorKind.RateLimited,
        408 or >= 500 => ProviderErrorKind.Unavailable,
        >= 400 => ProviderErrorKind.BadRequest,
        _ => ProviderErrorKind.Unknown,
    };

    private static string BuildMessage(ProviderErrorKind kind, string provider, string? detail)
    {
        var template = kind switch
        {
            ProviderErrorKind.InvalidKey => CoreStrings.ErrorInvalidKey,
            ProviderErrorKind.MissingKey => CoreStrings.ErrorMissingKey,
            ProviderErrorKind.RateLimited => CoreStrings.ErrorRateLimited,
            ProviderErrorKind.Unavailable => CoreStrings.ErrorUnavailable,
            ProviderErrorKind.NoNetwork => CoreStrings.ErrorNoNetwork,
            ProviderErrorKind.BadRequest when !string.IsNullOrWhiteSpace(detail) => CoreStrings.ErrorBadRequest,
            _ => CoreStrings.ErrorUnknown,
        };
        return string.Format(CultureInfo.CurrentCulture, template, provider, detail);
    }
}
