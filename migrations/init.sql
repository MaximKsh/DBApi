CREATE TABLE public."user"
(
    ID SERIAL PRIMARY KEY NOT NULL,
    about VARCHAR(1000),
    email VARCHAR(255),
    fullname VARCHAR(300),
    nickname VARCHAR(100) NOT NULL
);
CREATE UNIQUE INDEX user_email_uindex ON public."user" (email);
CREATE UNIQUE INDEX user_nickname_uindex ON public."user" (nickname);

CREATE TABLE public.forum
(
    ID SERIAL PRIMARY KEY NOT NULL,
    slug VARCHAR(300) NOT NULL,
    title VARCHAR(300),
    user_ID INT NOT NULL,
    CONSTRAINT forum_user_id_fk FOREIGN KEY (user_ID) REFERENCES "user" (id)
);
CREATE UNIQUE INDEX "forum_user_ID_uindex" ON public.forum (user_ID);

CREATE TABLE public.thread
(
    ID SERIAL PRIMARY KEY NOT NULL,
    author_ID INT NOT NULL,
    created TIMESTAMP DEFAULT now(),
    forum_ID INT NOT NULL,
    message VARCHAR(1000),
    slug VARCHAR(300),
    title VARCHAR(300),
    votes INT,
    CONSTRAINT thread_user_id_fk FOREIGN KEY (author_ID) REFERENCES "user" (id),
    CONSTRAINT thread_forum_id_fk FOREIGN KEY (forum_ID) REFERENCES forum (id)
);
CREATE UNIQUE INDEX "thread_slug_uindex" ON public.thread (slug);

CREATE TABLE public.post
(
    ID SERIAL PRIMARY KEY NOT NULL,
    author_ID INT NOT NULL,
    created TIMESTAMP DEFAULT now() NOT NULL,
    forum_ID INT NOT NULL,
    isEdited BOOLEAN DEFAULT FALSE ,
    message VARCHAR(1000),
    parent_ID INT DEFAULT 0 NOT NULL,
    thread_ID INT NOT NULL,
    CONSTRAINT post_user_id_fk FOREIGN KEY (author_ID) REFERENCES "user" (id),
    CONSTRAINT post_forum_id_fk FOREIGN KEY (forum_ID) REFERENCES forum (id),
    CONSTRAINT post_thread_id_fk FOREIGN KEY (thread_ID) REFERENCES thread (id)
);