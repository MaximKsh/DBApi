using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Runtime.Serialization.Json;
using Microsoft.AspNetCore.Mvc;
using KashirinDBApi.Controllers.DataContracts;
using Npgsql;
using KashirinDBApi.Controllers.Extensions;
using KashirinDBApi.Controllers.Helpers;
using NpgsqlTypes;

namespace KashirinDBApi.Controllers
{
    public class ThreadController : Controller
    {
    #region sql
        private static readonly string sqlPreselectThreadAndForum = @"
            select
                t.ID,
                f.ID,
                f.slug
            from thread t
            inner join forum f on t.forum_id = f.id
            where 
                {0}
            ;
        ";

        private static readonly string sqlInsertPost = @"
            insert into post(author_id, forum_id, message, parent_id, thread_id)
                (
                    select
                        *
                    from
                    (
                        select
                            (
                                select ID
                                from ""user""
                                where nickname = @nickname
                                limit 1
                            ) AS author_id,
                            @forum_id AS forum_id,
                            @message AS message,
                            case when @parent_id=0
                            then 0
                            else (select ID from post where ID = @parent_id limit 1)
                            end  as parent_id,
                            @thread_id as thread_id
                    ) t
                    where t.author_id is not null
                            and t.parent_id is not null
                )
            returning ID, author_id, created, forum_id, isedited, message, parent_id, thread_id
            ;

        ";


        private static readonly string sqlSelectThreadDetails = @"
            select
                u.nickname as author,
                t.created,
                f.slug as forum,
                t.id,
                t.message,
                t.slug,
                t.title,
                (select sum(vote) from vote where thread_id = t.id) as votes
            from thread t
            inner join ""user"" u on t.author_id = u.id
            inner join forum f on t.forum_id = f.id
            where {0} ;
        ";

        private static readonly string sqlUpdateThreadDetails = @"
            with ins as
            (
                update thread
                set
                    {0}
                    id = id
                where {1}
                returning id, author_id, created, forum_id, message, slug, title, votes
            )
            select
                u.nickname as author,
                i.created,
                f.slug as forum,
                i.id,
                i.message,
                i.slug,
                i.title,
                (select sum(vote) from vote where thread_id = i.id) as votes
            from ins i
            inner join ""user"" u on i.author_id = u.id
            inner join forum f on i.forum_id = f.id
            ;
        ";

        private static readonly string sqlSelectPosts = @"
            select
                *
            from (
            with recursive recursetree (
                id,
                path,
                author_id,
                created,
                forum_id,
                isedited,
                message,
                parent_id,
                thread_id) as (
                    select
                        id,
                        array_append('{}' :: INT [], id),
                        author_id,
                        created,
                        forum_id,
                        isedited,
                        message,
                        parent_id,
                        thread_id
                    from post
                    where
                        parent_id = 0
                    union all
                    select
                        p2.id,
                        array_append(path, p2.id),
                        p2.author_id,
                        p2.created,
                        p2.forum_id,
                        p2.isedited,
                        p2.message,
                        p2.parent_id,
                        p2.thread_id
                    from post p2
                    inner join recursetree rt on rt.id = p2.parent_id
            )
            select
                id,
                array_to_string(path, '.') AS path,
                author_id,
                created,
                forum_id,
                isedited,
                message,
                parent_id,
                thread_id
            from recursetree
            -- order by path if tree or parent tree, created if flat
            {0}
            --order by path
            --order by created
            )a
            -- where a.path > '12.20'
            {1}
            --
        
        ";


        private static readonly string sqlInsertVote = @"
            with tuple as
            (
                select
                    {0} as thread_id,
                    (select id from ""user"" where nickname = @author) as user_id,
                    @vote as vote
            ),
            ins as
            (
                insert into vote(thread_id, user_id, vote)
                    select thread_id, user_id, vote from tuple where user_id is not null and thread_id is not null
                on conflict(thread_id, user_id) do update set
                    vote = @vote
                returning thread_id
            )
            select
                u.nickname as author,
                t.created,
                f.slug as forum,
                t.id,
                t.message,
                t.slug,
                t.title,
                0 as votes
            from thread t
            inner join ""user"" u on t.author_id = u.id
            inner join forum f on t.forum_id = f.id
            where
                t.id = (select thread_id from ins limit 1)
            ;
        ";

         private static readonly string sqlAggregateVotes = @"
            select sum(vote) from vote where thread_id = @thread_id;
         "; 

    #endregion


    #region Fields
        private readonly IConfiguration Configuration;
    #endregion
    #region Constructor
        public ThreadController(IConfiguration Configuration)
        {
            this.Configuration = Configuration;
        }
    #endregion

        private void PreselectThreadAndForum(
            NpgsqlConnection conn,
            string slug_or_id,
            out long? thread_id,
            out long? forum_id,
            out string forum_slug)
        {
            long id;
            string slug = string.Empty;
            // Если параметром передан id, то в id будет число
            // Иначе в slug будет передан аргумент и в id поставлено -1
            if( !long.TryParse(slug_or_id, out id) )
            {
                id = -1;
                slug = slug_or_id;
            }

            // Преселект треда и форума
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = string.Format(
                    sqlPreselectThreadAndForum,
                    id != -1 ?
                        "t.ID = @id":
                        "t.slug = @slug"
                );
                cmd.Parameters.Add( 
                    id != -1 ?
                        new NpgsqlParameter("@id", id){ NpgsqlDbType = NpgsqlDbType.Integer }:
                        new NpgsqlParameter("@slug", slug)
                );

                using(var reader = cmd.ExecuteReader())
                {
                    if(reader.Read())
                    {
                        thread_id = reader.GetInt32(0);
                        forum_id = reader.GetInt32(1);
                        forum_slug = reader.GetValueOrDefault(2, "");
                    }
                    else
                    {
                        thread_id = null;
                        forum_id = null;
                        forum_slug = "";
                    }
                }
            }
        }

        private bool InsertPost(
            NpgsqlConnection conn,
            PostDetailsDataContract post,
            long thread_id,
            long forum_id,
            string forumSlug,
            List<PostDetailsDataContract> createdPosts)
        {
            bool success = false;
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = sqlInsertPost;
                cmd.Parameters.Add(Helper.NewNullableParameter("@nickname", post.Author));
                cmd.Parameters.Add(Helper.NewNullableParameter("@forum_id", forum_id, NpgsqlDbType.Integer));
                cmd.Parameters.Add(Helper.NewNullableParameter("@thread_id", thread_id, NpgsqlDbType.Integer));
                cmd.Parameters.Add(Helper.NewNullableParameter("@parent_id", post.Parent, NpgsqlDbType.Integer));
                cmd.Parameters.Add(Helper.NewNullableParameter("@message", post.Message));

                using(var reader = cmd.ExecuteReader())
                {
                    if(reader.Read())
                    {
                        var createdPost = new PostDetailsDataContract();
                        createdPost.ID = reader.GetInt32(0);
                        createdPost.Author = post.Author;
                        createdPost.Created = reader.GetDateTime(2).ToString();
                        createdPost.Thread = thread_id;
                        createdPost.Forum = forumSlug;
                        createdPost.IsEdited = reader.GetBoolean(4);
                        createdPost.Message = reader.GetValueOrDefault(5, "");
                        createdPost.Parent = reader.GetInt32(6);
                        createdPosts.Add(createdPost);

                        success = true;
                    }
                }
            }
            return success;
        }



        [Route("api/thread/{slug_or_id}/create")]
        [HttpPost]
        public JsonResult Create(string slug_or_id)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(List<PostDetailsDataContract>));
            var posts = (List<PostDetailsDataContract>)js.ReadObject(Request.Body);

            var createdPosts = new List<PostDetailsDataContract>();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                long? threadID, forumID;
                string forumSlug;
                PreselectThreadAndForum(
                    conn,
                    slug_or_id, 
                    out threadID, 
                    out forumID,
                    out forumSlug);

                if( threadID.HasValue 
                    && forumID.HasValue)
                {
                    var transaction = conn.BeginTransaction();
                    foreach(var post in posts)
                    {
                        var success = InsertPost(conn, 
                            post,
                            threadID.Value,
                            forumID.Value,
                            forumSlug,
                            createdPosts);
                        if(!success)
                        {
                            break;
                        }
                    }
                    if(posts.Count == createdPosts.Count)
                    {
                        transaction.Commit();
                        Response.StatusCode = 201;
                    }
                    else
                    {
                        transaction.Rollback();
                        Response.StatusCode = 409;
                    }
                }
                else
                {
                    Response.StatusCode = 404;
                }
            }

            return new JsonResult( Response.StatusCode == 201 ? (object)createdPosts : string.Empty);
        }


        [Route("api/thread/{slug_or_id}/details")]
        [HttpPost]
        public JsonResult DetailsPost(string slug_or_id)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(ThreadUpdateDataContract));
            var threadUpdate = (ThreadUpdateDataContract)js.ReadObject(Request.Body);
            var updatedThread = new ThreadDetailsDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    
                    string updateFields = string.Empty;
                    if(threadUpdate.Title != null)
                    {
                        updateFields += "title = @title,\n";
                        cmd.Parameters.Add(new NpgsqlParameter("@title", threadUpdate.Title));
                    }
                    if(threadUpdate.Message != null)
                    {
                        updateFields += "message = @message,\n";
                        cmd.Parameters.Add(new NpgsqlParameter("@message", threadUpdate.Message));
                    }

                    long id;
                    bool isID = false;
                    cmd.CommandText = string.Format(
                            sqlUpdateThreadDetails,
                            updateFields,
                            (isID = long.TryParse(slug_or_id, out id)) ?
                                "id = @id":
                                "slug = @slug"
                        );
                    cmd.Parameters.Add(
                            isID ?
                                new NpgsqlParameter("@id", id):
                                new NpgsqlParameter("@slug", slug_or_id));
                    
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            updatedThread.Author = reader.GetValueOrDefault(0, "");
                            updatedThread.Created = reader.GetTimeStamp(1).DateTime.ToString();
                            updatedThread.Forum = reader.GetValueOrDefault(2, "");
                            updatedThread.ID = reader.GetInt32(3);
                            updatedThread.Message = reader.GetValueOrDefault(4, "");
                            updatedThread.Slug = reader.GetValueOrDefault(5, "");
                            updatedThread.Title = reader.GetValueOrDefault(6, "");
                            updatedThread.Votes = reader.GetValueOrDefault(7, 0);
                            Response.StatusCode = 200;
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }
                }
            }
            return new JsonResult(Response.StatusCode == 200  ? updatedThread as object : string.Empty);
        
        }

        [Route("api/thread/{slug_or_id}/details")]
        [HttpGet]
        public JsonResult DetailsGet(string slug_or_id)
        {
            ThreadDetailsDataContract thread = new ThreadDetailsDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    
                    long id;
                    bool isID = false;
                    cmd.CommandText = string.Format(
                            sqlSelectThreadDetails,
                            (isID = long.TryParse(slug_or_id, out id)) ?
                                "t.id = @id":
                                "t.slug = @slug"
                        );
                    cmd.Parameters.Add(
                            isID ?
                                new NpgsqlParameter("@id", id):
                                new NpgsqlParameter("@slug", slug_or_id));
                    
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            thread.Author = reader.GetValueOrDefault(0, "");
                            thread.Created = reader.GetTimeStamp(1).DateTime.ToString();
                            thread.Forum = reader.GetValueOrDefault(2, "");
                            thread.ID = reader.GetInt32(3);
                            thread.Message = reader.GetValueOrDefault(4, "");
                            thread.Slug = reader.GetValueOrDefault(5, "");
                            thread.Title = reader.GetValueOrDefault(6, "");
                            thread.Votes = reader.GetValueOrDefault(7, 0);
                            Response.StatusCode = 200;
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }
                }
            }
            return new JsonResult(Response.StatusCode == 200  ? thread as object : string.Empty);
        }


        [Route("api/thread/{slug_or_id}/posts")]
        [HttpGet]
        public JsonResult Posts(
            string slug_or_id, 
            int? limit,
            string marker, 
            string sort = "flat",
            bool desc = false )
        {

            if(sort == "flat")
            {

            }
            else if(sort == "tree")
            {

            }
            else if(sort == "parent_tree")
            {

            }
            else
            {
                Response.StatusCode = 400;
            }

            return new JsonResult( "" );
        }
        
        [Route("api/thread/{slug_or_id}/vote")]
        [HttpPost]
        public JsonResult Vote(string slug_or_id)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(VoteDataContract));
            var vote = (VoteDataContract)js.ReadObject(Request.Body);
            var updatedThread = new ThreadDetailsDataContract();
             
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    
                    long id;
                    bool isID = false;
                    cmd.CommandText = string.Format(
                            sqlInsertVote,
                            (isID = long.TryParse(slug_or_id, out id)) ?
                                "@thread_id":
                                "(select ID from thread where slug = @thread_slug)"
                        );
                    cmd.Parameters.Add(
                            isID ?
                                new NpgsqlParameter("@thread_id", id):
                                new NpgsqlParameter("@thread_slug", slug_or_id));
                    cmd.Parameters.Add(new NpgsqlParameter("@author", vote.Nickname));
                    cmd.Parameters.Add(new NpgsqlParameter("@vote", vote.Voice > 0 ? 1 : -1));
                    
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            updatedThread.Author = reader.GetValueOrDefault(0, "");
                            updatedThread.Created = reader.GetTimeStamp(1).DateTime.ToString();
                            updatedThread.Forum = reader.GetValueOrDefault(2, "");
                            updatedThread.ID = reader.GetInt32(3);
                            updatedThread.Message = reader.GetValueOrDefault(4, "");
                            updatedThread.Slug = reader.GetValueOrDefault(5, "");
                            updatedThread.Title = reader.GetValueOrDefault(6, "");
                            updatedThread.Votes = reader.GetValueOrDefault(7, 0);
                            Response.StatusCode = 200;
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }
                }
                if(Response.StatusCode == 200)
                {
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = sqlAggregateVotes;
                        cmd.Parameters.Add( new NpgsqlParameter("@thread_id", updatedThread.ID) );
                        updatedThread.Votes = (long)cmd.ExecuteScalar();
                    }
                }
                
            }

            return new JsonResult( Response.StatusCode == 200  ? updatedThread as object : string.Empty );
        }
        
    }
}