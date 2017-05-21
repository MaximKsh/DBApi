
CREATE TABLE "user"
(
    ID SERIAL PRIMARY KEY NOT NULL,
    about TEXT,
    email VARCHAR(255),
    fullname VARCHAR(300),
    nickname VARCHAR(100) NOT NULL
);
CREATE UNIQUE INDEX user_email_uindex ON public."user" (lower(email));
CREATE UNIQUE INDEX user_nickname_uindex ON public."user" (lower(nickname));

CREATE TABLE forum
(
    ID SERIAL PRIMARY KEY NOT NULL,
    slug VARCHAR(300) NOT NULL,
    title VARCHAR(300),
    user_ID INT NOT NULL,
    user_name VARCHAR(300),
    posts INT NOT NULL DEFAULT 0,
    threads INT NOT NULL DEFAULT 0,
    CONSTRAINT forum_user_id_fk FOREIGN KEY (user_ID) REFERENCES "user" (id)
);
CREATE UNIQUE INDEX "forum_slug_uindex" ON public.forum (lower(slug));

CREATE TABLE thread
(
    ID SERIAL PRIMARY KEY NOT NULL,
    author_ID INT NOT NULL,
    author_name varchar(300) not null,
    created TIMESTAMP WITH TIME ZONE DEFAULT now(),
    forum_ID INT NOT NULL,
    forum_slug varchar(300) not null,
    message TEXT,
    slug VARCHAR(300),
    title VARCHAR(300),
    votes int default 0,
    CONSTRAINT thread_user_id_fk FOREIGN KEY (author_ID) REFERENCES "user" (id),
    CONSTRAINT thread_forum_id_fk FOREIGN KEY (forum_ID) REFERENCES forum (id)
);
CREATE UNIQUE INDEX "thread_slug_uindex" ON public.thread (lower(slug));
create index "thread_author_id_index" on public.thread(author_id);
create index "thread_forum_id_created_index" on public.thread(forum_id, created);

CREATE TABLE post
(
    ID SERIAL PRIMARY KEY NOT NULL,
    author_ID INT NOT NULL,
    author_name varchar(300) not null,
    created TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL,
    forum_ID INT NOT NULL,
    forum_slug varchar(300) not null,
    isEdited BOOLEAN DEFAULT FALSE ,
    message TEXT,
    parent_ID INT,
    path INT[] default '{}'::INT[],
    thread_ID INT NOT NULL,
    thread_slug varchar(300),
    CONSTRAINT post_user_id_fk FOREIGN KEY (author_ID) REFERENCES "user" (id),
    CONSTRAINT post_forum_id_fk FOREIGN KEY (forum_ID) REFERENCES forum (id),
    CONSTRAINT post_thread_id_fk FOREIGN KEY (thread_ID) REFERENCES thread (id)
);
create index "post_author_id_index" on public.post(author_id);
create index "post_forum_id_index" on public.post(forum_id);
create index "post_thread_id_parent_id_index" on public.post(thread_id, parent_id);
create index "post_created_index" on public.post(created);


CREATE TABLE vote
(
    id SERIAL PRIMARY KEY NOT NULL,
    thread_id INT NOT NULL,
    user_id INT NOT NULL,
    vote INT NOT NULL,
    CONSTRAINT vote_thread_id_fk FOREIGN KEY (thread_id) REFERENCES thread (id),
    CONSTRAINT vote_user_id_fk FOREIGN KEY (user_id) REFERENCES "user" (id)
);
CREATE UNIQUE INDEX vote_thread_id_user_id_uindex ON public.vote (thread_id, user_id);