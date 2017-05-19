using System;
using System.Runtime.Serialization.Json;
using Microsoft.AspNetCore.Mvc;
using KashirinDBApi.Controllers.DataContracts;
using Npgsql;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using KashirinDBApi.Controllers.Extensions;
using KashirinDBApi.Controllers.Helpers;
using KashirinDBApi.Controllers.SqlConstants;
using System.Threading.Tasks;

namespace KashirinDBApi.Controllers
{
    public class UserController : Controller
    {
        #region Fields
        private readonly IConfiguration Configuration;
        #endregion
        #region Constructor
        public UserController(IConfiguration Configuration)
        {
            this.Configuration = Configuration;
        }
        #endregion

        public string Index(string name)
        {
            return "empty";
        }

        [Route("api/User/{name}/Create")]
        [HttpPost]
        public async Task<JsonResult> Create(string name)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(UserProfileDataContract));
            var userProfile = (UserProfileDataContract)js.ReadObject(Request.Body);
            var outputProfiles = new List<UserProfileDataContract>();
            bool isInserted = true;
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = UserSqlConstants.SqlInsertUser;
                    cmd.Parameters.Add(
                        Helper.NewNullableParameter("@about", userProfile.About));
                    cmd.Parameters.Add(
                        Helper.NewNullableParameter("@email", userProfile.Email));
                    cmd.Parameters.Add(
                        Helper.NewNullableParameter("@fullname", userProfile.Fullname));
                    cmd.Parameters.Add(new NpgsqlParameter("@nickname", name));

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while(await reader.ReadAsync())
                        {
                            if(isInserted 
                                && reader.GetString(0) == "selected")
                            {
                                isInserted = false;
                            }
                            
                            outputProfiles.Add(
                                new UserProfileDataContract
                                {
                                    About = reader.GetValueOrDefault(2, ""),
                                    Email = reader.GetValueOrDefault(3, ""),
                                    Fullname = reader.GetValueOrDefault(4, ""),
                                    Nickname = reader.GetString(5),
                                }
                            );
                        }
                    }
                    
                }
            }
            Response.StatusCode = isInserted ? 201 : 409;
            return new JsonResult(isInserted ? outputProfiles[0] as object : outputProfiles as object);
        }

        [Route("api/User/{name}/Profile")]
        [HttpPost]
        public async Task<JsonResult> ProfilePost(string name)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(UserProfileDataContract));
            var userProfile = (UserProfileDataContract)js.ReadObject(Request.Body);
            
            UserProfileDataContract profile = null;
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = userProfile.Email != null ?
                                      UserSqlConstants.SqlUpdateProfileWithEmailConflictChecking:
                                      UserSqlConstants.SqlUpdateProfileWithoutConstraintChecking;
                    cmd.Parameters.Add(new NpgsqlParameter("@nickname", name));
                    cmd.Parameters.Add(Helper.NewNullableParameter("@email", userProfile.Email));
                    cmd.Parameters.Add(Helper.NewNullableParameter("@about", userProfile.About));
                    cmd.Parameters.Add(Helper.NewNullableParameter("@fullname", userProfile.Fullname));

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if(await reader.ReadAsync())
                        {
                            profile = new UserProfileDataContract();
                            profile.About = reader.GetValueOrDefault(0, "");
                            profile.Email = reader.GetValueOrDefault(1, "");
                            profile.Fullname = reader.GetValueOrDefault(2, "");
                            profile.Nickname = reader.GetString(3);
                            
                            Response.StatusCode = reader.GetString(4) == "updated" ?
                                                      200:
                                                      409;
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }
                }
            }

            return new JsonResult( Response.StatusCode == 200 ?  profile as object : string.Empty );
        }

        [Route("api/User/{name}/Profile")]
        [HttpGet]
        public async Task<JsonResult> ProfileGet(string name)
        {
            UserProfileDataContract profile = null;
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = UserSqlConstants.SqlSelectProfile;
                    cmd.Parameters.Add(new NpgsqlParameter("@name", name));
                    
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if(await reader.ReadAsync())
                        {
                            profile = new UserProfileDataContract();
                            profile.About = reader.GetValueOrDefault(0, "");
                            profile.Email = reader.GetValueOrDefault(1, "");
                            profile.Fullname = reader.GetValueOrDefault(2, "");
                            profile.Nickname = reader.GetString(3);
                        }
                    }
                }
            }
            Response.StatusCode = profile != null ? 200 : 404;
            return new JsonResult(profile != null  ? profile as object : string.Empty);
        }
        
    }
}