namespace MirraCloud.Core.Errors
{
    /// <summary>
    /// Convenience helpers for inspecting the typed-error envelope carried by
    /// <see cref="RestApiError"/>. Returns sane defaults when the backend
    /// payload was not a recognised cloud error response (network failure,
    /// legacy endpoint, etc.).
    /// </summary>
    public static class RestApiErrorExtensions
    {
        /// <summary>
        /// True if the error envelope contains an item with the given
        /// <paramref name="code"/>. Returns false if <paramref name="error"/>
        /// is null or <see cref="RestApiError.Errors"/> was not populated.
        /// </summary>
        public static bool HasCode(this RestApiError error, string code)
        {
            if (error == null || error.Errors == null)
            {
                return false;
            }

            for (var i = 0; i < error.Errors.Count; i++)
            {
                if (error.Errors[i] != null && error.Errors[i].Code == code)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// First error item with the given <paramref name="code"/>, or null
        /// when no such item exists (or the envelope was not parsed).
        /// </summary>
        public static CloudApiError GetByCode(this RestApiError error, string code)
        {
            if (error == null || error.Errors == null)
            {
                return null;
            }

            for (var i = 0; i < error.Errors.Count; i++)
            {
                if (error.Errors[i] != null && error.Errors[i].Code == code)
                {
                    return error.Errors[i];
                }
            }
            return null;
        }

        /// <summary>
        /// First error item in the envelope, or null when the envelope was
        /// not parsed. Useful for generic fallback handling.
        /// </summary>
        public static CloudApiError FirstCloudError(this RestApiError error)
        {
            if (error == null || error.Errors == null || error.Errors.Count == 0)
            {
                return null;
            }
            return error.Errors[0];
        }
    }
}
