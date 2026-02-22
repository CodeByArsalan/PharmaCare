using System.Text;
using XSystem.Security.Cryptography;

namespace PharmaCare.Web.Utilities;

public static class Utility
{
    public static string EncryptURL(string strData)
    {
        try
        {
            if (!string.IsNullOrEmpty(strData))
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
        catch (Exception)
        {
            return "";
        }
    }

    /// <summary>
    /// Extension method to encrypt an integer ID for use in URLs.
    /// </summary>
    public static string EncryptId(this int id)
    {
        return EncryptURL(id.ToString());
    }

    /// <summary>
    /// Decrypt an encrypted ID string back to integer.
    /// </summary>
    public static int DecryptId(string encryptedId)
    {
        var decrypted = DecryptURL(encryptedId);
        if (int.TryParse(decrypted, out int id))
            return id;
        return 0;
    }

    /// <summary>
    /// Extension method to encrypt a long ID for use in URLs.
    /// </summary>
    public static string EncryptId(this long id)
    {
        return EncryptURL(id.ToString());
    }

    /// <summary>
    /// Decrypt an encrypted ID string back to long.
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
        catch (Exception)
        {
            return "";
        }
    }
}
