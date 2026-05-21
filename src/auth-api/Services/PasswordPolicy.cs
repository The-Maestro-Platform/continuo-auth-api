namespace AuthApi.Services;

public static class PasswordPolicy {
    public static bool Validate(string password, out string error) {
        if (string.IsNullOrWhiteSpace(password)) {
            error = "PasswordRequired";
            return false;
        }

        if (password.Length < 12) {
            error = "PasswordTooShort";
            return false;
        }

        if (password.Any(char.IsWhiteSpace)) {
            error = "PasswordCannotContainSpaces";
            return false;
        }

        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSymbol = password.Any(ch => !char.IsLetterOrDigit(ch));

        if (!hasUpper || !hasLower || !hasDigit || !hasSymbol) {
            error = "PasswordTooWeak";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

