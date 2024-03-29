using Microsoft.AspNetCore.Mvc;
using KashirinDBApi.Controllers.DataContracts;
using Npgsql;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace KashirinDBApi.Controllers
{
    public class ServiceController : Controller
    {
       
        #region sql
        
        private static readonly string sqlClear = @"
            truncate table ""user"" cascade;
        ";

        private static readonly string sqlStatus = @"
            select 
                (select count(ID) from ""forum""),
                (select count(ID) from ""post""),
                (select count(ID) from ""thread""),
                (select count(ID) from ""user"")
            ;
        ";

        #endregion

        #region Fields
        private readonly IConfiguration Configuration;
        #endregion
        #region Constructor
        public ServiceController(IConfiguration Configuration)
        {
            this.Configuration = Configuration;
        }
        #endregion

        [Route("api/service/ping")]
        [HttpGet]
        public string Ping()
        {
            return "I am working";
        }
        [Route("api/service/error")]
        [HttpGet]
        public string Error()
        {
            return "Some error occured";
        }

        [Route("api/service/clear")]
        [HttpPost]
        public async void Clear()
        {
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = sqlClear;
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            Response.StatusCode = 200;
        }

        [Route("api/service/status")]
        [HttpGet]
        public async Task<JsonResult> Status()
        {
            ServiceStatusDataContract status = new ServiceStatusDataContract
            {
                Forum = 0,
                Post = 0,
                Thread = 0,
                User = 0
            };
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = sqlStatus;
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if(await reader.ReadAsync())
                        {
                            status.Forum = reader.GetInt32(0);
                            status.Post = reader.GetInt32(1);
                            status.Thread = reader.GetInt32(2);
                            status.User = reader.GetInt32(3);
                        }
                    }
                }
            }

            return new JsonResult(status);
        }
        
    }
}