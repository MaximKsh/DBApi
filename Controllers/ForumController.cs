
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

        [Route("api/forum/create")]
        [HttpPost]
        public JsonResult Create()
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
                conn.Open();
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
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
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
        public JsonResult CreateThread(string slug)
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
                conn.Open();
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
                        Helper.NewNullableParameter("@created", thread.Created, NpgsqlDbType.Timestamp));
                    cmd.Parameters.Add(
                        Helper.NewNullableParameter("@slug", thread.Slug));
                    cmd.Parameters.Add(
                        Helper.NewNullableParameter("@title", thread.Title));
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            Response.StatusCode = reader.GetString(0) == "inserted" ?
                                                    201 :
                                                    409;
                            
                            
                            newThread.ID = reader.GetInt32(1);
                            newThread.Author = reader.GetValueOrDefault(2, "");
                            newThread.Created = reader
                                        .GetTimeStamp(3)
                                        .DateTime
                                        .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
                            newThread.Forum = reader.GetValueOrDefault(4, "");
                            newThread.Message = reader.GetValueOrDefault(5, "");
                            newThread.Slug = reader.GetValueOrDefault<string>(6, null);
                            newThread.Title = reader.GetValueOrDefault(7, "");
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }
                    
                }
            }


            return new JsonResult(Response.StatusCode != 404 ? (object)newThread : string.Empty);
        }

        [Route("api/forum/{slug}/details")]
        [HttpGet]
        public JsonResult Details(string slug)
        {
            ForumDetailsDataContract details = new ForumDetailsDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = ForumSqlConstants.SqlSelectForumDetails;
                    cmd.Parameters.Add(new NpgsqlParameter("@slug", slug));
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
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
        public JsonResult Threads(string slug, int? limit, string since, bool desc = false)
        {
            List<ThreadDetailsDataContract> threads = new List<ThreadDetailsDataContract>();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                var transaction = conn.BeginTransaction();
                int? preselectedID = null;
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = ForumSqlConstants.SqlGetForumBySlug;
                    cmd.Parameters.Add(new NpgsqlParameter("@slug", slug));
                    preselectedID = (int?)cmd.ExecuteScalar();
                }
                if(preselectedID.HasValue)
                {
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = String.Format(
                            ForumSqlConstants.SqlSelectForumThreads,
                            since != null ? $"and created {(desc ? "<=" : ">=")} @since": "",
                            desc ? "desc" : "",
                            limit.HasValue && 0 < limit ? $"limit @limit" : ""
                        );
                        cmd.Parameters.Add(new NpgsqlParameter("@id", preselectedID.Value));
                        if(since != null)
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@since", since){ NpgsqlDbType = NpgsqlDbType.Timestamp });
                        }
                        if(limit.HasValue && 0 < limit)
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@limit", limit));
                        }
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var thread = new ThreadDetailsDataContract()
                                {
                                    Author = reader.GetValueOrDefault(0, ""),
                                    Created = reader
                                            .GetTimeStamp(1)
                                            .DateTime
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                                    Forum = reader.GetValueOrDefault(2, ""),
                                    ID = reader.GetInt32(3),
                                    Message = reader.GetValueOrDefault(4, ""),
                                    Slug = reader.GetValueOrDefault(5, ""),
                                    Title = reader.GetValueOrDefault(6, ""),
                                    Votes = reader.GetValueOrDefault(7, 0)
                                };
                                threads.Add(thread);
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
            return new JsonResult(Response.StatusCode == 200  ? threads as object : string.Empty);
        }

        [Route("api/forum/{slug}/users")]
        [HttpGet]
        public JsonResult Users(string slug, int? limit, string since, bool desc = false)
        {
            List<UserProfileDataContract> users = new List<UserProfileDataContract>();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                long? forumID = null;
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = ForumSqlConstants.SqlGetForumBySlug;
                    cmd.Parameters.Add(new NpgsqlParameter("@slug", slug));
                    forumID = (int?)cmd.ExecuteScalar();
                }

                if(forumID.HasValue)
                {
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = String.Format(
                            ForumSqlConstants.SqlSelectForumUsers,
                            since != null ? $"and convert_to(lower(u.nickname), 'utf8') {(desc ? "<" : ">")} convert_to(lower(@since), 'utf8')": "",
                            desc ? "desc" : ""
                        );
                        cmd.Parameters.Add(new NpgsqlParameter("@forum_id", forumID.Value));
                        if(since != null)
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@since", since));
                        }
                        cmd.Parameters.Add(new NpgsqlParameter("@limit", limit ?? int.MaxValue));
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
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