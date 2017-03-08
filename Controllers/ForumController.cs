
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

namespace KashirinDBApi.Controllers
{
    public class ForumController : Controller
    {
       
    #region sql
        
        private static readonly string sqlGetForumBySlug = @"
            select
                ID
            from forum
            where lower(slug) = lower(@slug);
        ";

        private static readonly string sqlInsertForum = @"
            with
                tuple as (
                    select
                        a.slug,
                        a.title,
                        u.id as user_id,
                        u.nickname nickname
                    from
                    (
                        select
                            @slug as slug,
                            @title as title
                    ) a 
                    inner join ""user"" u on lower(nickname) = lower(@nickname)
                ),
                ins as (
                    insert into forum (slug, title, user_id)
                        select slug, title, user_id from tuple where user_id is not null
                    on conflict do nothing
                    returning id, slug, title, user_id
                )
            select 'inserted' AS status, ins.id, ins.slug, ins.title, ins.user_id, t1.nickname FROM ins
            cross join tuple t1
            union all
            select 'selected' AS status, f.id, f.slug, f.title, f.user_id, t.nickname
            from   tuple t
            inner join forum as f on f.user_id = t.user_id ;
        ";


        private static readonly string sqlInsertThread = @"
            WITH
                author as (
                    select ID, nickname from ""user"" where lower(nickname) = lower(@nickname) limit 1
                ),
                ff as (
                    select ID, slug from forum where lower(slug) = lower(@forum_slug) limit 1
                ),
                tuple AS (
                    SELECT
                        case when @created is not null
                            then @created
                            else now()
                        end as created,
                        @message as message,
                        @slug as slug,
                        @title as title,
                        (select ID from author) as author_id,
                        (select ID from ff) as forum_id
                ),
                ins AS (
                    INSERT INTO thread (created, message, slug, title, author_id, forum_id)
                        SELECT created, message, slug, title, author_id, forum_id FROM tuple WHERE forum_id IS NOT NULL and author_id is not null
                    ON CONFLICT DO NOTHING
                    RETURNING id, author_id, created, forum_id, message, slug, title
                )
            SELECT
                'inserted' AS status,
                id,
                (select nickname from author),
                created,
                (select slug from ff),
                message,
                slug,
                title,
                (select sum(vote) from vote where thread_id = id) as votes
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
                (select sum(vote) from vote where thread_id = th.id) as votes
            FROM tuple AS tu
            INNER JOIN thread AS th ON lower(th.slug) = lower(tu.slug)
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
               lower(f.slug) = lower(@slug)
            ;
        ";


        private static readonly string sqlSelectForumThreads = @"
        select
            u.nickname,
            t.created,
            f.slug,
            t.id,
            t.message,
            t.slug,
            t.title,
            t.votes
        from thread t
        inner join ""user"" u on t.author_id = u.ID
        inner join forum f on t.forum_id = f.id
        where
            f.id = @id
            {0}
        order by t.created {1}
        {2} 
        ;
        ";

        private static readonly string sqlPreselectForumBySlug = @"
            select
                ID
            from forum
            where lower(slug) = lower(@slug)
            limit 1
        ";
        private static readonly string sqlSelectForumUsers = @"
            select distinct
                u.about,
                u.email,
                u.fullname,
                u.nickname,
                convert_to(lower(u.nickname), 'utf8')
            from ""user"" u
            left join thread t on
                            u.ID = t.author_ID
                            and t.forum_id = @forum_id
                            
            left join post p on
                            u.ID = p.author_ID
                            and p.forum_id = @forum_id
                           
            where (p.ID is not null or t.ID is not null) {0}
            order by 
                convert_to(lower(u.nickname), 'utf8') {1}
            {2}
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
                    cmd.CommandText = sqlInsertThread;
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
        public JsonResult Threads(string slug, int? limit, string since, bool desc = false)
        {
            List<ThreadDetailsDataContract> threads = new List<ThreadDetailsDataContract>();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                int? preselectedID = null;
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = sqlGetForumBySlug;
                    cmd.Parameters.Add(new NpgsqlParameter("@slug", slug));
                    preselectedID = (int?)cmd.ExecuteScalar();
                }
                if(preselectedID.HasValue)
                {
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = String.Format(
                            sqlSelectForumThreads,
                            since != null ? $"and created {(desc ? "<=" : ">=")} '{since.Replace("'", "''")}'": "",
                            desc ? "desc" : "",
                            limit.HasValue && 0 < limit ? $"limit + {limit.Value}" : ""
                        );
                        cmd.Parameters.Add(new NpgsqlParameter("@id", preselectedID.Value));
                        
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
                    cmd.CommandText = sqlPreselectForumBySlug;
                    cmd.Parameters.Add(new NpgsqlParameter("@slug", slug));
                    using(var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            forumID = reader.GetInt32(0);
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }
                }

                if(forumID.HasValue)
                {
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = String.Format(
                            sqlSelectForumUsers,
                            since != null ? $"and convert_to(lower(u.nickname), 'utf8') {(desc ? "<" : ">")} convert_to(lower('{since.Replace("'", "''")}'), 'utf8')": "",
                            desc ? "desc" : "",
                            limit.HasValue && 0 < limit ? $"limit + {limit.Value}" : ""
                        );
                        cmd.Parameters.Add(new NpgsqlParameter("@forum_id", forumID.Value));
                        
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
                
            }
            return new JsonResult(Response.StatusCode == 200  ? users as object : string.Empty);
        }
    }
}