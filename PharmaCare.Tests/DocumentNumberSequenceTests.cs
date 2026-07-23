using PharmaCare.Application.Utilities;

namespace PharmaCare.Tests;

public class DocumentNumberSequenceTests
{
    [Fact]
    public void DatePrefix_UsesPrefixAndTodaysDate()
    {
        var prefix = DocumentNumberSequence.DatePrefix("SALE");

        Assert.StartsWith("SALE-", prefix);
        Assert.EndsWith("-", prefix);
        Assert.Equal($"SALE-{AppTime.Now:yyyyMMdd}-", prefix);
    }

    [Fact]
    public void Next_WithNoExistingNumber_StartsAtOne()
    {
        var result = DocumentNumberSequence.Next("SALE-20260724-", null);

        Assert.Equal("SALE-20260724-0001", result);
    }

    [Fact]
    public void Next_IncrementsLastNumber()
    {
        var result = DocumentNumberSequence.Next("SALE-20260724-", "SALE-20260724-0042");

        Assert.Equal("SALE-20260724-0043", result);
    }

    [Fact]
    public void Next_PadsToFourDigits()
    {
        var result = DocumentNumberSequence.Next("GRN-20260724-", "GRN-20260724-0009");

        Assert.Equal("GRN-20260724-0010", result);
    }

    [Fact]
    public void Next_GrowsBeyondFourDigits()
    {
        var result = DocumentNumberSequence.Next("GRN-20260724-", "GRN-20260724-9999");

        Assert.Equal("GRN-20260724-10000", result);
    }

    [Fact]
    public void Next_WithUnparseableLastNumber_FallsBackToOne()
    {
        var result = DocumentNumberSequence.Next("JV-20260724-", "JV-20260724-XXXX");

        Assert.Equal("JV-20260724-0001", result);
    }

    [Fact]
    public void Next_WithEmptyLastNumber_StartsAtOne()
    {
        var result = DocumentNumberSequence.Next("CN-20260724-", "");

        Assert.Equal("CN-20260724-0001", result);
    }
}
