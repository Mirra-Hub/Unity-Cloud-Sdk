namespace MirraCloud.Core.Auth
{
    public enum AuthResultStatus
    {
        Success = 0,
        Conflict = 1,

        /// <summary>
        /// Nothing is created or linked yet: a code went to the email (<see cref="GetAuthDataDto.EmailVerification"/>).
        /// Finish with <see cref="AuthenticationService.ConfirmPasswordRegistrationAsync"/> or
        /// <see cref="AuthenticationService.ConfirmPasswordLinkAsync"/>.
        /// </summary>
        EmailVerificationRequired = 2
    }
}

