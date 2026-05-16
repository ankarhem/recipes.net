using App.Identity;
using App.Identity.Ports;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Infrastructure.Identity;

public sealed class EmailService(EmailSettings settings, ILogger<EmailService> logger)
    : IEmailService
{
    public Task SendEmailVerificationAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default
    )
    {
        var link = BuildLink("verify-email", token);
        return SendAsync(
            email,
            "Verify your email address",
            $"""
             <h2>Welcome to Recipes!</h2>
             <p>Please verify your email address by clicking the link below:</p>
             <p><a href="{link}">Verify Email</a></p>
             <p>This link expires in 24 hours.</p>
             <p>If you didn't create an account, you can ignore this email.</p>
             """,
            cancellationToken
        );
    }

    public Task SendPasswordResetAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default
    )
    {
        var link = BuildLink("reset-password", token);
        return SendAsync(
            email,
            "Reset your password",
            $"""
             <h2>Password Reset Request</h2>
             <p>You requested a password reset. Click the link below to set a new password:</p>
             <p><a href="{link}">Reset Password</a></p>
             <p>This link expires in 1 hour.</p>
             <p>If you didn't request a password reset, you can ignore this email.</p>
             """,
            cancellationToken
        );
    }

    private async Task SendAsync(
        string email,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken
    )
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = settings.RequireTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.StartTlsWhenAvailable;

        try
        {
            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, socketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(settings.SmtpUser))
            {
                await client.AuthenticateAsync(settings.SmtpUser, settings.SmtpPass, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Recipient}", email);
            throw;
        }
    }

    private string BuildLink(string path, string token)
    {
        var baseUrl = settings.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/{path}?token={Uri.EscapeDataString(token)}";
    }
}
