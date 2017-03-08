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
using System.Linq;

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

        private static readonly string sqlPreselectAuthorID = @"
            select ID
            from ""user""
            where nickname = @nickname
            limit 1;
        ";


        private static readonly string sqlInsertPost = @"
            insert into post(author_id, created, forum_id, message, parent_id, thread_id)
                (
                    select
                        *
                    from
                    (
                        select
                            @author_id AS author_id,
                            case when @created is null
                                then now()
                                else @created
                            end as created,
                            @forum_id AS forum_id,
                            @message AS message,
                            case when @parent_id=0
                                then 0
                                else (select ID from post where ID = @parent_id and thread_id = @thread_id limit 1)
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

        private static readonly string sqlSelectPostsFlat = @"
            select
                *
            from
            (
                select
                    p.id,
                    u.nickname,
                    p.created,
                    p.isedited,
                    p.message,
                    p.parent_id,
                    row_number() over (order by created {0}, p.id {0}) as rn
                from post p
                inner join ""user"" u on u.id = p.author_id
                where p.thread_id = @id
                order by created {0}, p.id {0} -- asc desc
            ) t
            where 
                @from <= rn and rn < @to
        ;";

        // Чтобы прикрутить сортировку на одном уровне по дате
        // Вместо order by full_path
        // Нужно order by true_path , parent_id , created
        private static readonly string sqlSelectPosts = @"
            select *
            from 
            (
                with recursive
                    recursetree (
                                id,
                                full_path,
                                author_id,
                                created,
                                forum_id,
                                isedited,
                                message,
                                parent_id,
                                thread_id) as
                (
                    select
                        id,
                        array_append('{1}' :: INT [], id),
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
                        and thread_id = @id
                    union all
                    select
                        p2.id,
                        array_append(full_path, p2.id),
                        p2.author_id,
                        p2.created,
                        p2.forum_id,
                        p2.isedited,
                        p2.message,
                        p2.parent_id,
                        p2.thread_id
                    from post p2
                    inner join recursetree rt on rt.id = p2.parent_id 
                                             and p2.thread_id = @id
                )
                select
                    p.id,
                    u.nickname,
                    created,
                    isedited,
                    message,
                    parent_id,
                    case when array_length(full_path, 1) = 1
                        then full_path
                        else array_remove(full_path, full_path[array_length(full_path, 1)])
                    end as true_path,
                    row_number() over (order by full_path {0}) as rn
                from recursetree p 
                inner join ""user"" u on author_id = u.id    
                order by full_path {0} 
            ) t
            where 
                @from <= rn and rn < @to
        ";

        // И снова, нужно будет нормально, то сортируем по created
        private static readonly string sqlSelectParentPosts = @"
            select *
            from
            (
                select
                    id,
                    row_number() over (order by id {0}) as rn
                from post
                where
                    parent_id = 0
                    and thread_id = @id
                order by id {0}
            )t
            where @from <= rn and rn < @to
        ";

        // Для сортировки вместе с датой order by true_path {0}, created {0}
        private static readonly string sqlSelectPostsByParentID = @"
                with recursive
                    recursetree (
                                id,
                                full_path,
                                author_id,
                                created,
                                forum_id,
                                isedited,
                                message,
                                parent_id,
                                thread_id) as
                (
                    select
                        id,
                        array_append('{1}' :: INT [], id),
                        author_id,
                        created,
                        forum_id,
                        isedited,
                        message,
                        parent_id,
                        thread_id
                    from post
                    where
                        id in ( {2} )
                    union all
                    select
                        p2.id,
                        array_append(full_path, p2.id),
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
                    p.id,
                    u.nickname,
                    created,
                    isedited,
                    message,
                    parent_id,
                    case when array_length(full_path, 1) = 1
                        then full_path
                        else array_remove(full_path, full_path[array_length(full_path, 1)])
                    end
                    as true_path
                from recursetree p 
                inner join ""user"" u on author_id = u.id    
                order by full_path {0}
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
                        "lower(t.slug) = lower(@slug)"
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

        private bool PreselectAuthorID(
            NpgsqlConnection conn,
            string nickname,
            out long authorID)
        {
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = sqlPreselectAuthorID;
                cmd.Parameters.Add(new NpgsqlParameter("@nickname", nickname));

                using(var reader = cmd.ExecuteReader())
                {
                    if(reader.Read())
                    {
                        authorID = reader.GetInt32(0);
                    }
                    else
                    {
                        authorID = -1;
                    }
                }
            }
            return authorID != -1;
        }

        private bool InsertPost(
            NpgsqlConnection conn,
            PostDetailsDataContract post,
            long author_id,
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
                cmd.Parameters.Add(Helper.NewNullableParameter("@author_id", author_id, NpgsqlDbType.Integer));
                cmd.Parameters.Add(Helper.NewNullableParameter("@created", post.Created, NpgsqlDbType.Timestamp));
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
                        createdPost.Created = reader
                                        .GetTimeStamp(2)
                                        .DateTime
                                        .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
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


        private List<int> PreselectTopLevelPosts(
            NpgsqlConnection conn,
            long thread_id,
            long from,
            long to,
            bool desc)
        {
            List<int> posts = new List<int>();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = string.Format(
                    sqlSelectParentPosts,
                    desc ? 
                        "desc":
                        "");
                cmd.Parameters.Add( 
                        new NpgsqlParameter("@id", thread_id){ NpgsqlDbType = NpgsqlDbType.Integer }
                );
                cmd.Parameters.Add( 
                        new NpgsqlParameter("@from", from){ NpgsqlDbType = NpgsqlDbType.Integer }
                );
                cmd.Parameters.Add( 
                        new NpgsqlParameter("@to", to){ NpgsqlDbType = NpgsqlDbType.Integer }
                );

                using(var reader = cmd.ExecuteReader())
                {
                    while(reader.Read())
                    {
                        posts.Add(reader.GetInt32(0));
                    }
                }
            }
            return posts;
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
                        long authorID;
                        if(!PreselectAuthorID(conn, post.Author, out authorID))
                        {
                            Response.StatusCode = 404;
                            break;
                        }

                        if(!InsertPost(conn, 
                            post,
                            authorID,
                            threadID.Value,
                            forumID.Value,
                            forumSlug,
                            createdPosts))
                        {
                            Response.StatusCode = 409;
                            break;
                        }
                    }
                    if(posts.Count == createdPosts.Count)
                    {
                        Response.StatusCode = 201;
                        transaction.Commit();
                    }
                    else
                    {
                        transaction.Rollback();
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
                                "lower(slug) = lower(@slug)"
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
                            updatedThread.Created = reader
                                            .GetTimeStamp(1)
                                            .DateTime
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
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
                                "lower(t.slug) = lower(@slug)"
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
                            thread.Created = reader
                                            .GetTimeStamp(1)
                                            .DateTime
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
                            thread.Forum = reader.GetValueOrDefault(2, "");
                            thread.ID = reader.GetInt32(3);
                            thread.Message = reader.GetValueOrDefault(4, "");
                            thread.Slug = reader.GetValueOrDefault(5, "");
                            thread.Title = reader.GetValueOrDefault(6, "");
                            thread.Votes = reader.GetValueOrDefault(7, 0L);
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
            var postPage = new PostPageDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                long? thread_id, forum_id;
                string forum_slug;
                PreselectThreadAndForum(
                    conn, 
                    slug_or_id,
                    out thread_id,
                    out forum_id,
                    out forum_slug);
                if(!forum_id.HasValue 
                    || !thread_id.HasValue)
                {
                    Response.StatusCode = 404;
                    return new JsonResult(string.Empty);
                }


                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    if(sort == "flat")
                    {
                        int from = -1;
                        int to = Int32.MaxValue;
                        // Если был передан корректный маркер, 
                        // то он заполнится в трай парс
                        // иначе поставится 1
                        if(marker == null 
                            || !Int32.TryParse(marker, out from)
                            ||  from <= 0)
                        {
                            from = 1;
                        }
                        if(limit.HasValue)
                        {
                            to = from + limit.Value;
                        }

                        cmd.CommandText = string.Format(
                                sqlSelectPostsFlat,
                                desc ? 
                                    "desc":
                                    ""
                            );
                        cmd.Parameters.Add( new NpgsqlParameter("@id", thread_id) );
                        cmd.Parameters.Add( new NpgsqlParameter("@from", from));
                        cmd.Parameters.Add( new NpgsqlParameter("@to", to));
                        
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            postPage.Posts = new List<PostDetailsDataContract>();
                            int? lastRN = null; 
                            while(reader.Read())
                            {
                                postPage.Posts.Add(
                                    new PostDetailsDataContract()
                                    {
                                        ID = reader.GetInt32(0),
                                        Author = reader.GetString(1),
                                        Created = reader
                                                    .GetTimeStamp(2)
                                                    .DateTime
                                                    .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                                        Forum = forum_slug,
                                        IsEdited = reader.GetBoolean(3),
                                        Message = reader.GetValueOrDefault(4, ""),
                                        Parent = reader.GetValueOrDefault(5, 0),
                                        Thread = thread_id.Value
                                    }
                                );
                                lastRN = reader.GetInt32(6);
                            }
                            postPage.Marker = lastRN != null ?
                                                (lastRN.Value + 1).ToString():
                                                marker;
                        }
                    }
                    else if(sort == "tree")
                    {
                        int from = -1;
                        int to = Int32.MaxValue;
                        // Если был передан корректный маркер, 
                        // то он заполнится в трай парс
                        // иначе поставится 1
                        if(marker == null 
                            || !Int32.TryParse(marker, out from)
                            ||  from <= 0)
                        {
                            from = 1;
                        }
                        if(limit.HasValue)
                        {
                            to = from + limit.Value;
                        }


                        cmd.CommandText = string.Format(
                                sqlSelectPosts,
                                desc ? 
                                    "desc":
                                    "",
                                "{}"
                            );
                        cmd.Parameters.Add( new NpgsqlParameter("@id", thread_id) );
                        cmd.Parameters.Add( new NpgsqlParameter("@from", from));
                        cmd.Parameters.Add( new NpgsqlParameter("@to", to));

                        using (var reader = cmd.ExecuteReader())
                        {
                            postPage.Posts = new List<PostDetailsDataContract>();
                            int? lastRN = null; 
                            while(reader.Read())
                            {
                                postPage.Posts.Add(
                                    new PostDetailsDataContract()
                                    {
                                        ID = reader.GetInt32(0),
                                        Author = reader.GetString(1),
                                        Created = reader
                                                    .GetTimeStamp(2)
                                                    .DateTime
                                                    .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                                        Forum = forum_slug,
                                        IsEdited = reader.GetBoolean(3),
                                        Message = reader.GetValueOrDefault(4, ""),
                                        Parent = reader.GetValueOrDefault(5, 0),
                                        Thread = thread_id.Value
                                    }
                                );
                                lastRN = reader.GetInt32(7);
                            }
                            postPage.Marker = lastRN != null ?
                                                (lastRN.Value + 1).ToString():
                                                marker;
                        } 
                    }
                    else if(sort == "parent_tree")
                    {
                        int from = -1;
                        int to = Int32.MaxValue;
                        // Если был передан корректный маркер, 
                        // то он заполнится в трай парс
                        // иначе поставится 1
                        if(marker == null 
                            || !Int32.TryParse(marker, out from)
                            ||  from <= 0)
                        {
                            from = 1;
                        }
                        if(limit.HasValue)
                        {
                            to = from + limit.Value;
                        }

                        var parents = PreselectTopLevelPosts(conn, thread_id.Value, from, to, desc);

                        if( parents.Count != 0)
                        {
                            cmd.CommandText = string.Format(
                                    sqlSelectPostsByParentID,
                                    desc ? 
                                        "desc":
                                        "",
                                    "{}",
                                    string.Join(",", parents.Select(p => p.ToString()))
                                );

                            using (var reader = cmd.ExecuteReader())
                            {
                                postPage.Posts = new List<PostDetailsDataContract>();
                                while(reader.Read())
                                {

                                    postPage.Posts.Add(
                                        new PostDetailsDataContract()
                                        {
                                            ID = reader.GetInt32(0),
                                            Author = reader.GetString(1),
                                            Created = reader
                                                        .GetTimeStamp(2)
                                                        .DateTime
                                                        .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                                            Forum = forum_slug,
                                            IsEdited = reader.GetBoolean(3),
                                            Message = reader.GetValueOrDefault(4, ""),
                                            Parent = reader.GetValueOrDefault(5, 0),
                                            Thread = thread_id.Value
                                        }
                                    );
                                    
                                }
                                postPage.Marker = (from + parents.Count).ToString();
                            } 
                        }
                        else
                        {
                            postPage.Posts = new List<PostDetailsDataContract>();
                            postPage.Marker = from.ToString();
                        }

                    }
                    else
                    {
                        Response.StatusCode = 400;
                    }
                }
            }

            return new JsonResult( postPage );
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
                                "(select ID from thread where ID = @thread_id)":
                                "(select ID from thread where lower(slug) = lower(@thread_slug))"
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
                            updatedThread.Created = reader
                                            .GetTimeStamp(1)
                                            .DateTime
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
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