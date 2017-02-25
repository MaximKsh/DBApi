using Npgsql;

namespace KashirinDBApi.Controllers.Extensions
{
    public static class Extensions
    {
        public static T GetValueOrDefault<T>(this NpgsqlDataReader reader, int ordinal, T defValue)
        {
            return !reader.IsDBNull(ordinal) ?
                    (T)reader.GetValue(ordinal) : 
                    defValue;
        }

        public static T GetValueOrDefault<T>(this NpgsqlDataReader reader, string columnName, T defValue)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return !reader.IsDBNull(ordinal) ?
                    (T)reader.GetValue(ordinal) : 
                    defValue;
        }
    }   
}