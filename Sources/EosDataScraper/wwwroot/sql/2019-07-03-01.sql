
-- SEQUENCE: public.node_info_id_seq
-- DROP SEQUENCE public.node_info_id_seq;
CREATE SEQUENCE public.node_info_id_seq;
ALTER SEQUENCE public.node_info_id_seq OWNER TO eos_data_light_user;

-- Table: public.node_info
-- DROP TABLE public.node_info;
CREATE TABLE public.node_info
(
    id integer NOT NULL DEFAULT nextval('node_info_id_seq'::regclass),
    url text COLLATE pg_catalog."default",
    success_count integer NOT NULL,
    fail_count integer NOT NULL,
    elapsed_milliseconds integer NOT NULL,
    CONSTRAINT pk_node_info PRIMARY KEY (id)
)
WITH (OIDS = FALSE) TABLESPACE pg_default;
ALTER TABLE public.node_info OWNER to eos_data_light_user;


-- SEQUENCE: public.dapp_id_seq
-- DROP SEQUENCE public.dapp_id_seq;
CREATE SEQUENCE public.dapp_id_seq;
ALTER SEQUENCE public.dapp_id_seq OWNER TO eos_data_light_user;
-- Table: public.dapp
-- DROP TABLE public.dapp;
CREATE TABLE public.dapp
(
    id integer NOT NULL DEFAULT nextval('dapp_id_seq'::regclass),
    author text COLLATE pg_catalog."default",
    slug text COLLATE pg_catalog."default",
    description text COLLATE pg_catalog."default",
    title text COLLATE pg_catalog."default",
    url text COLLATE pg_catalog."default",
    category text COLLATE pg_catalog."default",
	tsv tsvector,
    CONSTRAINT pk_dapp PRIMARY KEY (id)
)
WITH (OIDS = FALSE) TABLESPACE pg_default;
ALTER TABLE public.dapp OWNER to eos_data_light_user;

CREATE INDEX tsv_idx ON public.dapp USING GIN (tsv);
CREATE TRIGGER tsvectorupdate BEFORE INSERT OR UPDATE ON public.dapp FOR EACH ROW EXECUTE PROCEDURE tsvector_update_trigger(tsv, 'pg_catalog.english', slug, title, description, url, category);


UPDATE public.dapp SET tsv = to_tsvector('english', coalesce(slug,'') || ' ' || coalesce(title,'') || ' ' || coalesce(description,'') || ' ' || coalesce(url,'') || ' ' || coalesce(category,''));


-- Table: public.dapp_contract
-- DROP TABLE public.dapp_contract;
CREATE TABLE public.dapp_contract
(
    contract numeric(20,0) NOT NULL,
    dapp_id integer NOT NULL,
    CONSTRAINT dapp_contract_ukey UNIQUE (dapp_id, contract)
)
WITH (OIDS = FALSE) TABLESPACE pg_default;
ALTER TABLE public.dapp_contract OWNER to eos_data_light_user;

-- Table: public.transfer
-- DROP TABLE public.transfer;
CREATE TABLE public.transfer
(
    block_num bigint NOT NULL,
    transaction_id bytea NOT NULL,
	action_num integer NOT NULL,

    action_contract numeric(20,0) NOT NULL,
    action_name text COLLATE pg_catalog."default",
	transaction_expiration timestamp without time zone NOT NULL,
    
	"from" numeric(20,0) NOT NULL,
    "to" numeric(20,0) NOT NULL,
    quantity numeric NOT NULL,
    token_name character varying(8) COLLATE pg_catalog."default" NOT NULL,
	memo_utf8 bytea,

    transaction_status smallint,
    close_block_num bigint,
	"timestamp" date NOT NULL
) PARTITION BY RANGE ("timestamp")
WITH (OIDS = FALSE)
TABLESPACE pg_default;
ALTER TABLE public.transfer OWNER to eos_data_light_user;          

-- Table: public.token
-- DROP TABLE public.token;
CREATE TABLE public.token
(
    block_num bigint NOT NULL,
    transaction_id bytea NOT NULL,
	action_num integer NOT NULL,
    
	action_contract numeric(20,0) NOT NULL,
    action_name text COLLATE pg_catalog."default",
    transaction_expiration timestamp without time zone NOT NULL,

    issuer numeric(20,0) NOT NULL,
    maximum_supply numeric NOT NULL,
    token_name character varying(8) COLLATE pg_catalog."default" NOT NULL,
    usd_rate numeric,
	eos_rate numeric,

    transaction_status smallint,
    close_block_num bigint
)
WITH (OIDS = FALSE) TABLESPACE pg_default;
ALTER TABLE public.token OWNER to eos_data_light_user;

-- Table: public.delayed_transaction
-- DROP TABLE public.delayed_transaction;
CREATE TABLE public.delayed_transaction
(
    block_num bigint NOT NULL,
    transaction_id bytea NOT NULL,
	transaction_status smallint NOT NULL,
	"timestamp" timestamp without time zone NOT NULL
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;
ALTER TABLE public.delayed_transaction OWNER to eos_data_light_user;

-- Table: public.service_state
-- DROP TABLE public.service_state;
CREATE TABLE public.service_state
(
	service_id integer NOT NULL,
    json text COLLATE pg_catalog."default",
    CONSTRAINT pk_service_state PRIMARY KEY (service_id)
)
WITH (OIDS = FALSE)
TABLESPACE pg_default;
ALTER TABLE public.service_state OWNER to eos_data_light_user;
INSERT INTO public.service_state(service_id, json) VALUES (1, '{"BlockId":1}');

-- DROP TABLE public.transfers_info;
CREATE TABLE public.transfers_info
(
    "timestamp" date,	
    action_contract numeric,
    token_name text COLLATE pg_catalog."default",
    contract numeric,
    "sum" numeric,
    "count" bigint,
    "type" smallint,
	from_count bigint,
	to_count bigint,
    CONSTRAINT transfer_info_pkey PRIMARY KEY ("timestamp", action_contract, token_name, contract, "type")
) WITH (OIDS = FALSE) TABLESPACE pg_default;
ALTER TABLE public.transfers_info OWNER to eos_data_light_user;

INSERT INTO public.service_state(service_id, json) VALUES (2, 'transfer_1970_01_01');
