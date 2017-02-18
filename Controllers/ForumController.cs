
using Microsoft.Extensions.Configuration;
using System.Runtime.Serialization.Json;
using Microsoft.AspNetCore.Mvc;
using KashirinDBApi.Controllers.DataContracts;
using Npgsql;
using System;
using KashirinDBApi.Controllers.Extensions;
using KashirinDBApi.Controllers.Helpers;

namespace KashirinDBApi.Controllers
{
    public class ForumController : Controller
    {
       
    #region sql
        
        private static readonly string sqlInsertForum = @"
            with
                tuple as (
                select
                    @slug as slug,
                    @title as title,
                    (select ID from ""user"" where nickname = @nickname limit 1) as user_id
                ),
                ins as (
                    insert into forum (slug, title, user_id)
                        select slug, title, user_id from tuple where user_id is not null
                    on conflict do nothing
                    returning id, slug, title, user_id
                )
            select 'inserted' AS status, id, slug, title, user_id FROM ins
            union all
            select 'selected' AS status, f.id, f.slug, f.title, f.user_id
            from   tuple t
            inner join forum as f on f.user_id = t.user_id ;
        ";


        private static readonly string sqlInsertThread = @"
            WITH
                author as (
                    select ID, nickname from ""user"" where nickname = @nickname limit 1
                ),
                forum as (
                    select ID, slug from forum where slug = @forum_slug limit 1
                ),
                tuple AS (
                    SELECT
                        @message as message,
                        @slug as slug,
                        @title as title,
                        (select ID from author) as author_id,
                        (select ID from forum) as forum_id
                ),
                ins AS (
                    INSERT INTO thread (message, slug, title, author_id, forum_id)
                        SELECT message, slug, title, author_id, forum_id FROM tuple WHERE forum_id IS NOT NULL and author_id is not null
                    ON CONFLICT DO NOTHING
                    RETURNING id, author_id, created, forum_id, message, slug, title, votes
                )
            SELECT
                'inserted' AS status,
                id,
                (select nickname from author),
                created,
                (select slug from forum),
                message,
                slug,
                title,
                votes
            FROM ins
            UNION ALL
            SELECT
                'selected' AS status,
                th.id,
                u.nickname,
                th.created,
                f.slug,
                th.message,
                th.slug,
                th.title,
                th.votes
            FROM tuple AS tu
            INNER JOIN thread AS th ON th.slug = tu.slug
            INNER JOIN ""user"" AS u ON u.ID = th.author_id
            INNER JOIN forum as f ON f.ID = th.forum_id;
        ";



        private static readonly string sqlSelectForumDetails = @"
            select
                (select count(ID) from post where forum_id = f.ID) as posts,
                slug,
                (select count(ID) from thread where forum_id = f.ID) as threads,
                title,
                u.nickname
            from forum f
            inner join ""user"" u on f.user_id = u.ID
            where
                f.slug = @slug
            ;
        ";
    #endregion

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
                    cmd.CommandText = sqlInsertForum;
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
                            newForum.User = forum.User;
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }
                    
                }
            }
            Console.WriteLine(Response.StatusCode);
            return new JsonResult(Response.StatusCode != 404 ? newForum as object : string.Empty) ;
        }

        [Route("api/forum/{slug}/create")]
        [HttpPost]
        public JsonResult CreateThread(string slug)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(ThreadDetailsDataContract));
            var thread = (ThreadDetailsDataContract)js.ReadObject(Request.Body);
            if(thread.Author == null 
                || thread.Forum == null)
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
                    cmd.CommandText = sqlInsertThread;
                    cmd.Parameters.Add(
                        new NpgsqlParameter("@nickname", thread.Author));
                    cmd.Parameters.Add(
                        new NpgsqlParameter("@forum_slug", slug));
                    cmd.Parameters.Add(
                        Helper.NewNullableParameter("@message", thread.Message));
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

                            newThread.ID = reader.GetValueOrDefault(1, -1);
                            newThread.Author = reader.GetValueOrDefault(2, "");
                            newThread.Created = reader.GetTimeStamp(3).DateTime.ToString();
                            newThread.Forum = reader.GetValueOrDefault(4, "");
                            newThread.Message = reader.GetValueOrDefault(5, "");
                            newThread.Slug = reader.GetValueOrDefault(6, "");
                            newThread.Title = reader.GetValueOrDefault(7, "");
                            newThread.Votes = reader.GetValueOrDefault(8, 0);
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
                    cmd.CommandText = sqlSelectForumDetails;
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
        public JsonResult Threads(string slug, int limit, DateTime since, bool desc = false)
        {

            
            return new JsonResult( "{\"вжух\": \"все форум slug threads \"}" );
        }

        [Route("api/forum/{slug}/users")]
        [HttpGet]
        public JsonResult Users(string slug)
        {
            return new JsonResult( "{\"вжух\": \"все форум slug usesr \"}" );
        }
    }
}