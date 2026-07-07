namespace PharmaCare.Application.Exceptions;

/// <summary>
/// Thrown when a pricing rule is violated (e.g. a sale price below cost). The message is
/// user-safe and intended to be surfaced directly to the operator.
/// </summary>
public class PricingValidationException : Exception
{
    public PricingValidationException(string message) : base(message)
    {
    }
}
