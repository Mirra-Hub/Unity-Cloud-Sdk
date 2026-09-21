using System;
using System.Collections.Generic;
using MirraCloud.Json;

namespace MirraCloud.Core.Errors
{
    /// <summary>
    /// Wire-shape parsed out of an error response body. Mirrors the backend
    /// contract <c>CloudShared.Errors.Contracts.ErrorResponseDto</c>.
    /// </summary>
    /// <remarks>
    /// Consumers should not use this type directly — the populated errors are
    /// surfaced on <see cref="RestApiError.Errors"/>. The DTO exists only as
    /// the deserialisation target for <c>RestApiClient</c>.
    /// <para>
    /// Every Cloud host writes the envelope in camelCase and the SDK's JSON mapper matches member names
    /// case-sensitively, so the members carry <see cref="JsonNameCamelAttribute"/> — without it
    /// <c>errors</c> never matched <see cref="Errors"/> and every failure arrived with no codes.
    /// </para>
    /// </remarks>
    [Serializable]
    internal sealed class ErrorResponseDto
    {
        [JsonNameCamel] public List<CloudApiError> Errors;
    }
}
