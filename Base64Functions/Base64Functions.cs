using System;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;
using System.Text;

public class Base64Functions
{
    [SqlFunction]
    public static SqlString EncodeBase64(SqlString input)
    {
        if (input.IsNull)
            return SqlString.Null;
        var bytes = Encoding.UTF8.GetBytes(input.Value);
        return Convert.ToBase64String(bytes);
    }

    [SqlFunction]
    public static SqlString DecodeBase64(SqlString input)
    {
        if (input.IsNull)
            return SqlString.Null;
        try
        {
            var bytes = Convert.FromBase64String(input.Value);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return SqlString.Null;
        }
    }
}
