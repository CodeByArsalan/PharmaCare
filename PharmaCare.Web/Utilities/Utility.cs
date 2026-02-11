using System.Text;
using XSystem.Security.Cryptography;

namespace PharmaCare.Web.Utilities;

public static class Utility
{
    public static string EncryptURL(string strData)
    {
        try
        {
            if (!String.IsNullOrEmpty(strData))
            {
                SHA1Managed shaM = new SHA1Managed();
                Convert.ToBase64String(shaM.ComputeHash(Encoding.ASCII.GetBytes(strData)));
                Byte[] encByteData;
                encByteData = ASCIIEncoding.ASCII.GetBytes(strData);
                String encStrData = Convert.ToBase64String(encByteData);
                return encStrData;
            }
            else
            {
                return "";
            }
        }
        catch (Exception) { return ""; }

    }

    /// <summary>
    /// Extension method to encrypt an integer ID for use in URLs
    /// </summary>
    public static string EncryptId(this int id)
    {
        return EncryptURL(id.ToString());
    }

    /// <summary>
    /// Decrypt an encrypted ID string back to integer
    /// </summary>
    public static int DecryptId(string encryptedId)
    {
        var decrypted = DecryptURL(encryptedId);
        if (int.TryParse(decrypted, out int id))
            return id;
        return 0;
    }

    /// <summary>
    /// Extension method to encrypt a long ID for use in URLs
    /// </summary>
    public static string EncryptId(this long id)
    {
        return EncryptURL(id.ToString());
    }

    /// <summary>
    /// Decrypt an encrypted ID string back to long
    /// </summary>
    public static long DecryptIdLong(string encryptedId)
    {
        var decrypted = DecryptURL(encryptedId);
        if (long.TryParse(decrypted, out long id))
            return id;
        return 0;
    }

    public static string DecryptURL(string strData)
    {
        try
        {
            if (!string.IsNullOrEmpty(strData))
            {
                Byte[] decByteData;
                decByteData = Convert.FromBase64String(strData);
                String decStrData = ASCIIEncoding.ASCII.GetString(decByteData);

                return decStrData;
            }
            else
            {
                return "";
            }

        }
        catch (Exception) { return ""; }
    }
    public static char ToChar(this object obj)
    {
        return Convert.ToChar(obj);
    }
    public static Int16 ToShortInt(this object obj)
    {
        return Convert.ToInt16(obj);
    }
    public static string ToString2(this object obj)
    {
        return Convert.ToString(obj);
    }
    public static int ToInt32(this object obj)
    {
        return Convert.ToInt32(obj);
    }
    public static byte ToByte(this object obj)
    {
        return Convert.ToByte(obj);
    }
    public static Int64 ToInt64(this object obj)
    {
        return Convert.ToInt64(obj);
    }
    public static decimal ToDecimal(this object obj)
    {
        return Convert.ToDecimal(obj);
    }
    public static Double ToDouble(this object obj)
    {
        return Convert.ToDouble(obj);
    }
    public static DateTime ToDate(this object obj)
    {
        return Convert.ToDateTime(obj);
    }
    public static DateTime ToFromDateTime(this object obj)
    {
        DateTime _date = Convert.ToDateTime(obj);
        return new DateTime(_date.Year, _date.Month, _date.Day, 0, 0, 0, 1);
    }
    public static DateTime ToToDateTime(this object obj)
    {
        DateTime _date = Convert.ToDateTime(obj);
        return new DateTime(_date.Year, _date.Month, _date.Day, 23, 59, 59, 999);
    }
    public static bool ToBool(this object obj)
    {
        try
        {
            return Convert.ToBoolean(obj);
        }
        catch
        {
            return false;
        }
    }
    public static string ToAmountInWords(this object Num)
    {
        string word = "";
        string Number = Num.ToString2();
        try
        {
            bool beginsZero = false;//tests for 0XX    
            bool isDone = false;//test if already translated    
            double dblAmt = (Convert.ToDouble(Number));
            //if ((dblAmt > 0) && number.StartsWith("0"))    
            if (dblAmt > 0)
            {//test for zero or digit zero in a nuemric    
                beginsZero = Number.StartsWith("0");

                int numDigits = Number.Length;
                int pos = 0;//store digit grouping    
                String place = "";//digit grouping name:hundres,thousand,etc...    
                switch (numDigits)
                {
                    case 1://ones' range    

                        word = ones(Number);
                        isDone = true;
                        break;
                    case 2://tens' range    
                        word = tens(Number);
                        isDone = true;
                        break;
                    case 3://hundreds' range    
                        pos = (numDigits % 3) + 1;
                        place = " Hundred ";
                        break;
                    case 4://thousands' range    
                    case 5:
                    case 6:
                        pos = (numDigits % 4) + 1;
                        place = " Thousand ";
                        break;
                    case 7://millions' range    
                    case 8:
                    case 9:
                        pos = (numDigits % 7) + 1;
                        place = " Million ";
                        break;
                    case 10://Billions's range    
                    case 11:
                    case 12:

                        pos = (numDigits % 10) + 1;
                        place = " Billion ";
                        break;
                    //add extra case options for anything above Billion...    
                    default:
                        isDone = true;
                        break;
                }
                if (!isDone)
                {//if transalation is not done, continue...(Recursion comes in now!!)    
                    if (Number.Substring(0, pos) != "0" && Number.Substring(pos) != "0")
                    {
                        try
                        {
                            word = ToAmountInWords(Number.Substring(0, pos)) + place + ToAmountInWords(Number.Substring(pos));
                        }
                        catch { }
                    }
                    else
                    {
                        word = ToAmountInWords(Number.Substring(0, pos)) + ToAmountInWords(Number.Substring(pos));
                    }

                    //check for trailing zeros    
                    //if (beginsZero) word = " and " + word.Trim();    
                }
                //ignore digit grouping names    
                if (word.Trim().Equals(place.Trim())) word = "";
            }
        }
        catch { }
        return word.Trim();
    }
    private static string tens(string Number)
    {
        int _Number = Convert.ToInt32(Number);
        string name = null;
        switch (_Number)
        {
            case 10:
                name = "Ten";
                break;
            case 11:
                name = "Eleven";
                break;
            case 12:
                name = "Twelve";
                break;
            case 13:
                name = "Thirteen";
                break;
            case 14:
                name = "Fourteen";
                break;
            case 15:
                name = "Fifteen";
                break;
            case 16:
                name = "Sixteen";
                break;
            case 17:
                name = "Seventeen";
                break;
            case 18:
                name = "Eighteen";
                break;
            case 19:
                name = "Nineteen";
                break;
            case 20:
                name = "Twenty";
                break;
            case 30:
                name = "Thirty";
                break;
            case 40:
                name = "Fourty";
                break;
            case 50:
                name = "Fifty";
                break;
            case 60:
                name = "Sixty";
                break;
            case 70:
                name = "Seventy";
                break;
            case 80:
                name = "Eighty";
                break;
            case 90:
                name = "Ninety";
                break;
            default:
                if (_Number > 0)
                {
                    name = tens(Number.Substring(0, 1) + "0") + " " + ones(Number.Substring(1));
                }
                break;
        }
        return name;
    }
    private static string ones(string Number)
    {
        int _Number = Convert.ToInt32(Number);
        string name = "";
        switch (_Number)
        {

            case 1:
                name = "One";
                break;
            case 2:
                name = "Two";
                break;
            case 3:
                name = "Three";
                break;
            case 4:
                name = "Four";
                break;
            case 5:
                name = "Five";
                break;
            case 6:
                name = "Six";
                break;
            case 7:
                name = "Seven";
                break;
            case 8:
                name = "Eight";
                break;
            case 9:
                name = "Nine";
                break;
        }
        return name;
    }
}
