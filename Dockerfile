FROM ubuntu:16.04

MAINTAINER Kashirin Maxim


# Обновление списка пакетов
RUN apt-get -y update

ENV PGVER 9.5
ENV WORK /opt/DBApi

# Обвновление списка пакетов
RUN apt-get install -y --no-install-recommends \
        postgresql-$PGVER \
        apt-transport-https \
        apt-utils \
        openssl \
        ca-certificates \ 
    && rm -rf /var/lib/apt/lists/*



RUN sh -c 'echo "deb [arch=amd64] https://apt-mo.trafficmanager.net/repos/dotnet-release/ xenial main" > /etc/apt/sources.list.d/dotnetdev.list'
RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 417A0893
RUN apt-get update
RUN apt-get -y install dotnet-dev-1.0.1


# Копируем исходный код в Docker-контейнер
ADD ./ $WORK/

#
# Установка postgresql
#
# Run the rest of the commands as the ``postgres`` user created by the ``postgres-$PGVER`` package when it was ``apt-get installed``
USER postgres

# Create a PostgreSQL role named ``docker`` with ``docker`` as the password and
# then create a database `docker` owned by the ``docker`` role.
RUN /etc/init.d/postgresql start &&\
    psql --command "CREATE USER docker WITH SUPERUSER PASSWORD 'docker';" &&\
    createdb -O docker docker &&\
    psql -f $WORK/migrations/init.sql docker &&\
    /etc/init.d/postgresql stop

# Adjust PostgreSQL configuration so that remote connections to the
# database are possible.
RUN echo "host all  all    0.0.0.0/0  md5" >> /etc/postgresql/$PGVER/main/pg_hba.conf

# And add ``listen_addresses`` to ``/etc/postgresql/$PGVER/main/postgresql.conf``
RUN echo "listen_addresses='*'" >> /etc/postgresql/$PGVER/main/postgresql.conf

# Expose the PostgreSQL port
EXPOSE 5432

# Add VOLUMEs to allow backup of config, logs and databases
VOLUME  ["/etc/postgresql", "/var/log/postgresql", "/var/lib/postgresql"]

# Back to the root user
USER root


EXPOSE 5000

WORKDIR $WORK
# Собираем проект
RUN dotnet migrate && dotnet restore && dotnet build
# Запуск
CMD service postgresql start && dotnet run
