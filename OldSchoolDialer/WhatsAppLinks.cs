namespace OldSchoolDialer;

public static class WhatsAppLinks
{
    public static string Chat(string number)
    {
        var digits = new string(number.Where(c => c is >= '0' and <= '9').ToArray());
        if (digits.Length == 9 && digits.StartsWith('9')) digits = "51" + digits;
        if (digits.Length is < 10 or > 15 || digits.StartsWith('0'))
            throw new InvalidOperationException("Usa un número válido con código de país.");
        return "https://wa.me/" + digits;
    }
}
