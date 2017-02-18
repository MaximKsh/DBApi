using System;
using Npgsql;
using NpgsqlTypes;

namespace KashirinDBApi.Controllers.Helpers
{
    public static class Helper
    {
        public static NpgsqlParameter NewNullableParameter(
            string varName, 
            object value, 
            NpgsqlDbType type = NpgsqlDbType.Text)
        {
            return new NpgsqlParameter(varName, value ?? DBNull.Value)
                    {
                        IsNullable = true,
                        NpgsqlDbType = type
                    };
        }
    }
}