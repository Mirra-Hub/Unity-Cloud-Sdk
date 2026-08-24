using System;
using System.Collections.Generic;

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
    /// </remarks>
    [Serializable]
    internal sealed class ErrorResponseDto
    {
        public List<CloudApiError> Errors;
    }
}
