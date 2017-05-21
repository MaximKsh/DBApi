
using Microsoft.Extensions.Configuration;
using System.Runtime.Serialization.Json;
using Microsoft.AspNetCore.Mvc;
using KashirinDBApi.Controllers.DataContracts;
using Npgsql;
using System;
using KashirinDBApi.Controllers.Extensions;
using KashirinDBApi.Controllers.Helpers;
using System.Collections.Generic;
using NpgsqlTypes;
using KashirinDBApi.Controllers.SqlConstants;
using System.Threading.Tasks;

namespace KashirinDBApi.Controllers
{
    public class ForumController : Controller
    {

        #region Fields
        private readonly IConfiguration Configuration;
        #endregion
        #region Constructor
        public ForumController(IConfiguration Configuration)
        {
            this.Configuration = Configuration;
        }
        #endregion

        private async Task UpdateThreadCount(NpgsqlConnection conn, int forumID)
        {
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = ForumSqlConstants.SqlUpdateThreadCount;
                cmd.Parameters.Add(new NpgsqlParameter("@id", forumID));
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task UpdateForumUsers(NpgsqlConnection conn, long forumID, long userID)
        {
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = ForumSqlConstants.SqlInsertForumUsers;
                cmd.Parameters.Add(new NpgsqlParameter("@forum_ID", forumID));
                cmd.Parameters.Add(new NpgsqlParameter("@user_ID", userID));
                await cmd.ExecuteNonQueryAsync();
            }
        }

        [Route("api/forum/create")]
        [HttpPost]
        public async Task<JsonResult> Create()
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(ForumDetailsDataContract));
            var forum = (ForumDetailsDataContract)js.ReadObject(Request.Body);
            if(forum.User == null 
                || forum.Slug == null)
            {
                Response.StatusCode = 400;
                return new JsonResult(string.Empty);
            }

            ForumDetailsDataContract newForum  = new ForumDetailsDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = ForumSqlConstants.SqlInsertForum;
                    cmd.Parameters.Add(
                        new NpgsqlParameter("@slug", forum.Slug));
                    cmd.Parameters.Add(
                        Helper.NewNullableParameter("@title", forum.Title));
                    cmd.Parameters.Add(
                        new NpgsqlParameter("@nickname",forum.User));
                    
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if(await reader.ReadAsync())
                        {
                            Response.StatusCode = reader.GetString(0) == "inserted" ?
                                                    201 :
                                                    409;
                            
                            newForum.Slug = reader.GetValueOrDefault(2, "");
                            newForum.Title = reader.GetValueOrDefault(3, "");
                            newForum.User = reader.GetValueOrDefault(5, "");
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }  
                }
            }
            return new JsonResult(Response.StatusCode != 404 ? newForum as object : string.Empty) ;
        }

        [Route("api/forum/{slug}/create")]
        [HttpPost]
        public async Task<JsonResult> CreateThread(string slug)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(ThreadDetailsDataContract));
            var thread = (ThreadDetailsDataContract)js.ReadObject(Request.Body);
            if(thread.Author == null)
            {
                Response.StatusCode = 400;
                return new JsonResult(string.Empty);
            }

            ThreadDetailsDataContract newThread = new ThreadDetailsDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                int? forumID = -1;
                int authorID = -1;
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = ForumSqlConstants.SqlInsertThread;
                    cmd.Parameters.Add(
                        new NpgsqlParameter("@nickname", thread.Author));
                    cmd.Parameters.Add(
                        new NpgsqlParameter("@forum_slug", slug));
                    cmd.Parameters.Add(
                        Helper.NewNullableParameter("@message", thread.Message));
                    cmd.Parameters.Add(
                        Helper.NewNullableParameter("@created", thread.Created != null ? DateTime.Parse(thread.Created).ToUniversalTime() : DateTime.UtcNow, NpgsqlDbType.TimestampTZ));
                    cmd.Parameters.Add(
                        Helper.NewNullableParameter("@slug", thread.Slug));
                    cmd.Parameters.Add(
                        Helper.NewNullableParameter("@title", thread.Title));
                    
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if(await reader.ReadAsync())
                        {
                            Response.StatusCode = reader.GetString(0) == "inserted" ?
                                                    201 :
                                                    409;
                            
                            
                            newThread.ID = reader.GetInt32(1);
                            newThread.Author = reader.GetValueOrDefault(2, "");
                            newThread.Created = reader
                                        .GetDateTime(3)
                                        .ToUniversalTime()
                                        .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                            newThread.Forum = reader.GetValueOrDefault(4, "");
                            newThread.Message = reader.GetValueOrDefault(5, "");
                            newThread.Slug = reader.GetValueOrDefault<string>(6, null);
                            newThread.Title = reader.GetValueOrDefault(7, "");
                            forumID = reader.GetInt32(9);
                            authorID = reader.GetInt32(10);
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }  
                }
                if(Response.StatusCode == 201)
                {
                    await UpdateThreadCount(conn, forumID.Value);
                    await UpdateForumUsers(conn, forumID.Value, authorID);
                }
            }

            return new JsonResult(Response.StatusCode != 404 ? (object)newThread : string.Empty);
        }

        [Route("api/forum/{slug}/details")]
        [HttpGet]
        public async Task<JsonResult> Details(string slug)
        {
            ForumDetailsDataContract details = new ForumDetailsDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = ForumSqlConstants.SqlSelectForumDetails;
                    cmd.Parameters.Add(new NpgsqlParameter("@slug", slug));
                    
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if(await reader.ReadAsync())
                        {
                            details.Posts = reader.GetInt64(0);
                            details.Slug = reader.GetValueOrDefault(1, "");
                            details.Threads = reader.GetInt64(2);
                            details.Title = reader.GetValueOrDefault(3, "");
                            details.User = reader.GetValueOrDefault(4, "");
                            Response.StatusCode = 200;
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }
                }
            }
            return new JsonResult(Response.StatusCode == 200  ? details as object : string.Empty);
        }
        
        [Route("api/forum/{slug}/threads")]
        [HttpGet]
        public async Task<JsonResult> Threads(string slug, int? limit, string since, bool desc = false)
        {
            List<ThreadDetailsDataContract> threads = new List<ThreadDetailsDataContract>();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                int? preselectedID = null;
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = ForumSqlConstants.SqlGetForumBySlug;
                    cmd.Parameters.Add(new NpgsqlParameter("@slug", slug));
                    preselectedID = (int?) await cmd.ExecuteScalarAsync();
                }
                if(preselectedID.HasValue)
                {
                    Response.StatusCode = 200;
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = desc ? 
                                          ForumSqlConstants.SqlSelectForumThreadsDesc:
                                          ForumSqlConstants.SqlSelectForumThreadsAsc; 
                        cmd.Parameters.Add(new NpgsqlParameter("@id", preselectedID.Value));
                        cmd.Parameters.Add(Helper.NewNullableParameter("@since", since, NpgsqlDbType.Timestamp));
                        cmd.Parameters.Add(new NpgsqlParameter("@limit", limit));
                        
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var thread = new ThreadDetailsDataContract()
                                {
                                    Author = reader.GetValueOrDefault(0, ""),
                                    Created = reader
                                            .GetDateTime(1)
                                            .ToUniversalTime()
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                                    Forum = reader.GetValueOrDefault(2, ""),
                                    ID = reader.GetInt32(3),
                                    Message = reader.GetValueOrDefault(4, ""),
                                    Slug = reader.GetValueOrDefault(5, ""),
                                    Title = reader.GetValueOrDefault(6, ""),
                                    Votes = reader.GetValueOrDefault(7, 0)
                                };
                                threads.Add(thread);
                            }
                        }
                    }
                }
                else
                {
                    Response.StatusCode = 404;
                }
            }
            return new JsonResult(Response.StatusCode == 200  ? threads as object : string.Empty);
        }

        [Route("api/forum/{slug}/users")]
        [HttpGet]
        public async Task<JsonResult> Users(string slug, int? limit, string since, bool desc = false)
        {
            List<UserProfileDataContract> users = new List<UserProfileDataContract>();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                long? forumID = null;
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = ForumSqlConstants.SqlGetForumBySlug;
                    cmd.Parameters.Add(new NpgsqlParameter("@slug", slug));
                    forumID = (int?) await cmd.ExecuteScalarAsync();
                }

                if(forumID.HasValue)
                {
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = desc ?
                                          ForumSqlConstants.SqlSelectForumUsersDesc:
                                          ForumSqlConstants.SqlSelectForumUsersAsc;
                        cmd.Parameters.Add(new NpgsqlParameter("@forum_id", forumID.Value));
                        cmd.Parameters.Add(Helper.NewNullableParameter("@since", since));
                        cmd.Parameters.Add(new NpgsqlParameter("@limit", limit ?? int.MaxValue));
                        
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var user = new UserProfileDataContract()
                                {
                                    About = reader.GetValueOrDefault(0, ""),
                                    Email = reader.GetValueOrDefault(1, ""),
                                    Fullname = reader.GetValueOrDefault(2, ""),
                                    Nickname = reader.GetString(3)
                                };
                                users.Add(user);
                                Response.StatusCode = 200;
                            }
                        }
                    }
                }
                else
                {
                    Response.StatusCode = 404;
                }
                
            }
            return new JsonResult(Response.StatusCode == 200  ? users as object : string.Empty);
        }
    }
}