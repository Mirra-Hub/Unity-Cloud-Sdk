using System;
using MirraCloud.Core.Auth;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Asks for the code from a letter (<see cref="EmailVerificationDto"/>): where it went, the code field and a resend
    /// button. With <c>askNewPassword</c> it also takes a new password, for the password reset. Raises intents; the app
    /// layer performs the SDK calls.
    /// </summary>
    public sealed class EmailCodeForm : VisualElement
    {
        public event Action<string, string> Submitted;
        public event Action ResendRequested;

        private readonly Label _sentTo;

        public EmailCodeForm(EmailVerificationDto verification, string submitText, bool askNewPassword = false)
        {
            AddToClassList("sc-form");

            _sentTo = new Label();
            _sentTo.AddToClassList("sc-chat-hint");
            Add(_sentTo);
            Update(verification);

            var code = new TextField("Code") { maxLength = 6 };
            code.AddToClassList("sc-field");
            Add(code);

            TextField newPassword = null;
            if (askNewPassword)
            {
                newPassword = new TextField("New password") { isPasswordField = true };
                newPassword.AddToClassList("sc-field");
                Add(newPassword);
            }

            var submit = new Button(() => Submitted?.Invoke(code.value?.Trim(), newPassword?.value)) { text = submitText };
            submit.AddToClassList("sc-btn");
            submit.AddToClassList("sc-btn--block");
            submit.AddToClassList("sc-btn--primary");
            Add(submit);

            var resend = new Button(() => ResendRequested?.Invoke()) { text = "Send a new code" };
            resend.AddToClassList("sc-btn");
            resend.AddToClassList("sc-btn--block");
            resend.style.marginTop = 4;
            Add(resend);
        }

        /// <summary>Shows the request a resend answered with.</summary>
        public void Update(EmailVerificationDto verification)
        {
            var minutes = Math.Max(1, (verification?.ExpiresInSeconds ?? 0) / 60);
            _sentTo.text = string.IsNullOrEmpty(verification?.Email)
                ? "If the account exists, a code is on its way to its email."
                : $"We sent a code to {verification.Email}. It works for {minutes} min.";
        }
    }
}
