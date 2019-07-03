-- User: eos_data_light_user
-- DROP USER eos_data_light_user;
CREATE USER eos_data_light_user WITH
  LOGIN
  NOSUPERUSER
  INHERIT
  NOCREATEDB
  NOCREATEROLE
  NOREPLICATION;
  
alter user eos_data_light_user with encrypted password 'F4887A10-64EB-4F82-940F-169B01E491AC';

-- Database: eos_data_light
-- DROP DATABASE eos_data_light;
CREATE DATABASE eos_data_light
    WITH 
    OWNER = eos_data_light_user
    ENCODING = 'UTF8'
    TABLESPACE = pg_default
    CONNECTION LIMIT = -1;

-- SEQUENCE: public.version_id_seq
-- DROP SEQUENCE public.version_id_seq;
CREATE SEQUENCE public.version_id_seq;
ALTER SEQUENCE public.version_id_seq OWNER TO eos_data_light_user;
-- Table: public.version
-- DROP TABLE public.version;
CREATE TABLE public.version
(
    id integer NOT NULL DEFAULT nextval('version_id_seq'::regclass),
    name text COLLATE pg_catalog."default",
    CONSTRAINT pk_version PRIMARY KEY (id)
)
WITH (OIDS = FALSE) TABLESPACE pg_default;
ALTER TABLE public.version OWNER to eos_data_light_user;