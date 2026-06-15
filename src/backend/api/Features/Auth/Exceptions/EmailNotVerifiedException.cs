namespace SalesTrainer.Api.Features.Auth.Exceptions;

public sealed class EmailNotVerifiedException : Exception
{
    public EmailNotVerifiedException(string email)
        : base("Email address has not been verified.")
    {
        Email = email;
    }

    public string Email { get; }
}
