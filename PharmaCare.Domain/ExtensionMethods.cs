namespace PharmaCare.Domain;

public static class ExtensionMethods
{
    public static string ToAmountInWords(this decimal amount)
    {
        var wholeNumber = Math.Truncate(amount).ToString("0");
        return ToAmountInWordsCore(wholeNumber).Trim();
    }

    private static string ToAmountInWordsCore(string number)
    {
        var word = string.Empty;

        if (!double.TryParse(number, out var dblAmt) || dblAmt <= 0)
        {
            return word;
        }

        var isDone = false;
        var numDigits = number.Length;
        var pos = 0;
        var place = string.Empty;

        switch (numDigits)
        {
            case 1:
                word = Ones(number);
                isDone = true;
                break;
            case 2:
                word = Tens(number);
                isDone = true;
                break;
            case 3:
                pos = (numDigits % 3) + 1;
                place = " Hundred ";
                break;
            case 4:
            case 5:
            case 6:
                pos = (numDigits % 4) + 1;
                place = " Thousand ";
                break;
            case 7:
            case 8:
            case 9:
                pos = (numDigits % 7) + 1;
                place = " Million ";
                break;
            case 10:
            case 11:
            case 12:
                pos = (numDigits % 10) + 1;
                place = " Billion ";
                break;
            default:
                isDone = true;
                break;
        }

        if (!isDone)
        {
            word = number.Substring(0, pos) != "0" && number.Substring(pos) != "0"
                ? ToAmountInWordsCore(number.Substring(0, pos)) + place + ToAmountInWordsCore(number.Substring(pos))
                : ToAmountInWordsCore(number.Substring(0, pos)) + ToAmountInWordsCore(number.Substring(pos));

            if (word.Trim().Equals(place.Trim(), StringComparison.Ordinal))
            {
                word = string.Empty;
            }
        }

        return word;
    }

    private static string Tens(string number)
    {
        var parsed = Convert.ToInt32(number);
        return parsed switch
        {
            10 => "Ten",
            11 => "Eleven",
            12 => "Twelve",
            13 => "Thirteen",
            14 => "Fourteen",
            15 => "Fifteen",
            16 => "Sixteen",
            17 => "Seventeen",
            18 => "Eighteen",
            19 => "Nineteen",
            20 => "Twenty",
            30 => "Thirty",
            40 => "Fourty",
            50 => "Fifty",
            60 => "Sixty",
            70 => "Seventy",
            80 => "Eighty",
            90 => "Ninety",
            _ when parsed > 0 => Tens(number.Substring(0, 1) + "0") + " " + Ones(number.Substring(1)),
            _ => string.Empty
        };
    }

    private static string Ones(string number)
    {
        var parsed = Convert.ToInt32(number);
        return parsed switch
        {
            1 => "One",
            2 => "Two",
            3 => "Three",
            4 => "Four",
            5 => "Five",
            6 => "Six",
            7 => "Seven",
            8 => "Eight",
            9 => "Nine",
            _ => string.Empty
        };
    }
}
