using System.Net.Mail;
using System.Text.RegularExpressions;

namespace AgroConnect.Domain.Rules;

public static partial class AccountRules
{
    public static bool IsValidEmail(string email) =>
        email.Length <= 254 && EmailPattern().IsMatch(email) &&
        MailAddress.TryCreate(email, out var address) && address.Address == email;

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailPattern();
}
