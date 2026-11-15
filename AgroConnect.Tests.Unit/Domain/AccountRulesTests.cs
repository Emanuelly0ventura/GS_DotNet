using AgroConnect.Domain.Rules;
using Xunit;

namespace AgroConnect.Tests.Unit.Domain;

public sealed class AccountRulesTests
{
    [Theory]
    [InlineData("produtor@example.com", true)]
    [InlineData("produtor+demo@example.com", true)]
    [InlineData("produtor@localhost", false)]
    [InlineData("produtor @example.com", false)]
    [InlineData("Produtor <produtor@example.com>", false)]
    public void Registration_requires_mobile_email_format(string email, bool expected) =>
        Assert.Equal(expected, AccountRules.IsValidEmail(email));
}
