namespace PharmaCare.Application.Exceptions;

/// <summary>
/// Thrown when a credit sale would push a customer past their configured credit limit.
/// <para>
/// Distinct from other validation failures because it is OVERRIDABLE: the caller may re-issue the
/// same sale with <c>overrideCreditLimit: true</c> once an authorised user has confirmed. The web
/// layer decides who is allowed to do that; the service only decides whether the limit is breached.
/// </para>
/// The message is user-safe and intended to be shown to the operator.
/// </summary>
public class CreditLimitExceededException : Exception
{
    public decimal CreditLimit { get; }

    /// <summary>Balance the customer already owes, before this sale.</summary>
    public decimal CurrentOutstanding { get; }

    /// <summary>Unpaid portion this sale would add.</summary>
    public decimal AdditionalCredit { get; }

    /// <summary>What the customer would owe if this sale went through.</summary>
    public decimal ProjectedOutstanding => CurrentOutstanding + AdditionalCredit;

    public CreditLimitExceededException(
        string customerName,
        decimal creditLimit,
        decimal currentOutstanding,
        decimal additionalCredit)
        : base($"This sale would put {customerName} over their credit limit. " +
               $"Limit {creditLimit:N2}, already owing {currentOutstanding:N2}, " +
               $"this sale adds {additionalCredit:N2} on credit " +
               $"(total {(currentOutstanding + additionalCredit):N2}).")
    {
        CreditLimit = creditLimit;
        CurrentOutstanding = currentOutstanding;
        AdditionalCredit = additionalCredit;
    }
}
