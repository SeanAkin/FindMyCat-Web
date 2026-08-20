using FindMyCat.Core.Services;

namespace FindMyCat.UnitTests.Services;

public class PasswordPolicyTests
{
    [Fact]
    public void IsValid_MeetsAllRules_ReturnsTrue()
    {
        PasswordPolicy.IsValid("Str0ng!Pass").ShouldBeTrue();
    }

    [Theory]
    [InlineData("Sh0rt!")]
    [InlineData("nouppercase1!")]
    [InlineData("NoSymbolHere1")]
    public void IsValid_ViolatesARule_ReturnsFalse(string password)
    {
        PasswordPolicy.IsValid(password).ShouldBeFalse();
    }

    [Fact]
    public void GetViolations_TooShort_IncludesLengthViolation()
    {
        PasswordPolicy.GetViolations("Sh0rt!").ShouldContain(v => v.Contains("8 characters"));
    }

    [Fact]
    public void GetViolations_NoUppercase_IncludesUppercaseViolation()
    {
        PasswordPolicy.GetViolations("nouppercase1!").ShouldContain(v => v.Contains("uppercase"));
    }

    [Fact]
    public void GetViolations_NoSymbol_IncludesSymbolViolation()
    {
        PasswordPolicy.GetViolations("NoSymbolHere1").ShouldContain(v => v.Contains("symbol"));
    }

    [Fact]
    public void GetViolations_TooLong_IncludesLengthViolation()
    {
        var tooLong = "Str0ng!Pass" + new string('a', PasswordPolicy.MaximumLength);
        PasswordPolicy.GetViolations(tooLong).ShouldContain(v => v.Contains($"{PasswordPolicy.MaximumLength} characters"));
    }

    [Fact]
    public void IsValid_AtMaximumLength_ReturnsTrue()
    {
        var atMax = "Str0ng!" + new string('a', PasswordPolicy.MaximumLength - "Str0ng!".Length);
        atMax.Length.ShouldBe(PasswordPolicy.MaximumLength);
        PasswordPolicy.IsValid(atMax).ShouldBeTrue();
    }
}
