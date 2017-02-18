using System;
using System.Runtime.Serialization.Json;
using Microsoft.AspNetCore.Mvc;
using KashirinDBApi.Controllers.DataContracts;
using Npgsql;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace KashirinDBApi.Controllers
{
    public class UserController : Controller
    {
       
        #region sql

        private static readonly string sqlInsertUser = @"
            with tuple as (
                select
                    @about as about,
                    @email as email,
                    @fullname as fullname,
                    @nickname as nickname
                ),
                ins as (
                    insert into ""user"" (about, email, fullname, nickname)
                    select about, email, fullname, nickname from tuple
                    on conflict do nothing
                    returning id, about, email, fullname, nickname
                )
            select 'inserted' AS status, id, about, email, fullname, nickname FROM ins
            union all
            select 'selected' AS status, u.id, u.about, u.email, u.fullname, u.nickname
            from   tuple t
            inner join ""user"" u on u.email = t.email or u.nickname = t.nickname ;
        ";

        private static readonly string sqlSelectProfile = @"
            select 
                about,
                email,
                fullname,
                nickname
            from ""user""
            where 
                nickname = @name
            ;
        ";

        private static readonly string sqlUpdateProfileWithoutConstraintChecking = @"
            update 
                ""user""
            set 
                {0}
                id = id
            where
                nickname = @nickname
            returning about, email, fullname, nickname, 'updated'
            ;
        ";
        private static readonly string sqlUpdateProfileWithEmailConflictChecking = @"
            with same_email(ID) as
                (
                    select
                        ID
                    from ""user""
                    where
                        email = @email
                        and nickname <> @nickname
                ),
                upd as (
                    update
                        ""user""
                    set
                        email = case
                                    when exists(select * from same_email)
                                    then email
                                    else @email
                                 end,
                        {0}
                        id = id
                    where
                        nickname = @nickname
                    RETURNING about, email, fullname, nickname
                )
            select
                about,
                email,
                fullname,
                nickname,
                case
                    when exists(select * from same_email)
                    then 'conflicted'
                    else 'updated'
                end as status
            from upd;
        ";

        private static readonly string sqlUpdateAbout = "about = @about,";
        private static readonly string sqlUpdateFullname = "fullname = @fullname,";
        
        

        #endregion

        #region Fields
        private readonly IConfiguration Configuration;
        #endregion
        #region Constructor
        public UserController(IConfiguration Configuration)
        {
            this.Configuration = Configuration;
        }
        #endregion


        private void BuildQuery(UserProfileDataContract profile, string name, NpgsqlCommand cmd)
        {
            string queryResult = string.Empty;

            string baseQuery =  string.Empty; 
           
            cmd.Parameters.Add(new NpgsqlParameter("@nickname", name));
            if (profile.Email != null)
            {
                baseQuery = sqlUpdateProfileWithEmailConflictChecking;
                cmd.Parameters.Add(new NpgsqlParameter("@email", profile.Email));
            }
            else
            {
                baseQuery = sqlUpdateProfileWithoutConstraintChecking;
            }

            string subquery = string.Empty;
            if(profile.About != null)
            {
                 cmd.Parameters.Add(new NpgsqlParameter("@about", profile.About));
                 subquery += sqlUpdateAbout;
            }
            if(profile.Fullname != null)
            {
                cmd.Parameters.Add(new NpgsqlParameter("@fullname", profile.Fullname));
                subquery += sqlUpdateFullname;
            }
            
            queryResult = string.Format(
                    baseQuery,
                    subquery);
            cmd.CommandText =  queryResult;
        }

        public string Index(string name)
        {
            return "empty";
        }

        [Route("api/User/{name}/Create")]
        [HttpPost]
        public JsonResult Create(string name)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(UserProfileDataContract));
            var userProfile = (UserProfileDataContract)js.ReadObject(Request.Body);
            var outputProfiles = new List<UserProfileDataContract>();
            bool isInserted = true;
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    // Retrieve all rows
                    cmd.CommandText = sqlInsertUser;
                    cmd.Parameters.Add(new NpgsqlParameter("@about", (object)userProfile.About ?? DBNull.Value ));
                    cmd.Parameters.Add(new NpgsqlParameter("@email", (object)userProfile.Email ?? DBNull.Value));
                    cmd.Parameters.Add(new NpgsqlParameter("@fullname", (object)userProfile.Fullname ?? DBNull.Value));
                    cmd.Parameters.Add(new NpgsqlParameter("@nickname", name));
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        while(reader.Read())
                        {
                            if(isInserted 
                                && reader.GetString(0) == "selected")
                            {
                                isInserted = false;
                            }
                            
                            outputProfiles.Add(
                                new UserProfileDataContract
                                {
                                    About = reader.GetString(2),
                                    Email = reader.GetString(3),
                                    Fullname = reader.GetString(4),
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
        public JsonResult ProfilePost(string name)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(UserProfileDataContract));
            var userProfile = (UserProfileDataContract)js.ReadObject(Request.Body);
            
            UserProfileDataContract profile = null;
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    // Retrieve all rows
                    
                    BuildQuery(userProfile, name, cmd);

                    

                    using (var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            profile = new UserProfileDataContract();
                            profile.About = !reader.IsDBNull(0) ?
                                        reader.GetString(0) :
                                        "";
                            profile.Email = reader.GetString(1);
                            profile.Fullname = !reader.IsDBNull(2) ?
                                                reader.GetString(2) :
                                                "";
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
        public JsonResult ProfileGet(string name)
        {
            UserProfileDataContract profile = null;
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    // Retrieve all rows
                    cmd.CommandText = sqlSelectProfile;
                    cmd.Parameters.Add(new NpgsqlParameter("@name", name));
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            profile = new UserProfileDataContract();
                            profile.About = !reader.IsDBNull(0) ?
                                        reader.GetString(0) :
                                        "";
                            profile.Email = reader.GetString(1);
                            profile.Fullname = !reader.IsDBNull(2) ?
                                                reader.GetString(2) :
                                                "";
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