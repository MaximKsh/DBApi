
using Microsoft.Extensions.Configuration;
using System.Runtime.Serialization.Json;
using Microsoft.AspNetCore.Mvc;
using KashirinDBApi.Controllers.DataContracts;
using Npgsql;
using System;

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
            tuple AS (
            SELECT
                @message as message,
                @slug as slug,
                @title as title,
                (select ID from ""user"" where nickname = @nickname limit 1) as author_id,
                (select ID from forum where slug = @forum_slug limit 1) as forum_id
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
            author_id,
            created,
            forum_id,
            message,
            slug,
            title,
            votes
        FROM ins
        UNION ALL
        SELECT
            'selected' AS status,
            th.id,
            th.author_id,
            th.created,
            th.forum_id,
            th.message,
            th.slug,
            th.title,
            th.votes
        FROM tuple AS tu
        INNER JOIN thread AS th ON th.author_id = tu.author_id;
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
            ForumDetailsDataContract newForum  = new ForumDetailsDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    // Retrieve all rows
                    cmd.CommandText = sqlInsertForum;
                    cmd.Parameters.Add(new NpgsqlParameter("@slug", forum.Slug));
                    cmd.Parameters.Add(new NpgsqlParameter("@title", forum.Title));
                    cmd.Parameters.Add(new NpgsqlParameter("@nickname", forum.User));
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            Response.StatusCode = reader.GetString(0) == "inserted" ?
                                                    201 :
                                                    409;
                            
                            newForum.Slug = !reader.IsDBNull(2) ? 
                                              reader.GetString(2) : 
                                              "";
                            newForum.Title = !reader.IsDBNull(3) ? 
                                              reader.GetString(3) : 
                                              "";
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
            ThreadDetailsDataContract newThread = new ThreadDetailsDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    // Retrieve all rows
                    cmd.CommandText = sqlInsertThread;
                    cmd.Parameters.Add(new NpgsqlParameter("@name", thread.Author));
                    cmd.Parameters.Add(new NpgsqlParameter("@forum_slug", slug));
                    cmd.Parameters.Add(new NpgsqlParameter("@message", thread.Message));
                    cmd.Parameters.Add(new NpgsqlParameter("@slug", thread.Slug));
                    cmd.Parameters.Add(new NpgsqlParameter("@title", thread.Title));
                    using (var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            Response.StatusCode = reader.GetString(0) == "inserted" ?
                                                    201 :
                                                    409;
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }
                    
                }
            }


            return new JsonResult( "{\"вжух\": \"все форум криейт тред\"}" );
        }

        [Route("api/forum/{slug}/details")]
        [HttpGet]
        public JsonResult Details(string slug)
        {
            return new JsonResult( "{\"вжух\": \"все форум slug details \"}" );
        }
        
        [Route("api/forum/{slug}/threads")]
        [HttpGet]
        public JsonResult Threads(string slug, string limit, string since)
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