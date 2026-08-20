namespace PharmaCare.Domain.Enums;
public enum ActivityType
{
    Create = 1,
    Update = 2,
    Delete = 3,
    StatusChange = 4,
    Login = 5,
    Logout = 6,
    View = 7,

    /// <summary>
    /// A sign-in attempt that was refused — wrong password, deactivated account, or lockout.
    /// Recorded so the log can show a password-guessing run and explain why an account is locked.
    /// </summary>
    LoginFailed = 8
}
