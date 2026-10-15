--
-- PostgreSQL database dump
--

\restrict fukbVPBAxhIsBYRrVnQx4bLL8xtkZpSVC5EBrTZPlyY4uX9mKEKn8PPGTKjuiKc

-- Dumped from database version 17.11
-- Dumped by pg_dump version 17.11

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: public; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA IF NOT EXISTS public;


--
-- Name: SCHEMA public; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON SCHEMA public IS 'standard public schema';


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: assignments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.assignments (
    assignment_id integer NOT NULL,
    request_id integer NOT NULL,
    staff_id integer NOT NULL,
    assigned_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    active boolean DEFAULT true NOT NULL
);


--
-- Name: assignments_assignment_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.assignments ALTER COLUMN assignment_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.assignments_assignment_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: categories; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.categories (
    category_id integer NOT NULL,
    name character varying(100) NOT NULL,
    description text
);


--
-- Name: categories_category_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.categories ALTER COLUMN category_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.categories_category_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: request_actions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.request_actions (
    action_id integer NOT NULL,
    request_id integer NOT NULL,
    user_id integer NOT NULL,
    details text NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


--
-- Name: request_actions_action_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.request_actions ALTER COLUMN action_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.request_actions_action_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: resolutions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.resolutions (
    resolution_id integer NOT NULL,
    request_id integer NOT NULL,
    resolved_by integer NOT NULL,
    details text NOT NULL,
    resolved_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


--
-- Name: resolutions_resolution_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.resolutions ALTER COLUMN resolution_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.resolutions_resolution_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: service_requests; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.service_requests (
    request_id integer NOT NULL,
    requester_id integer NOT NULL,
    category_id integer NOT NULL,
    title character varying(200) NOT NULL,
    description text NOT NULL,
    current_status character varying(50) NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    version integer DEFAULT 1 NOT NULL
);


--
-- Name: service_requests_request_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.service_requests ALTER COLUMN request_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.service_requests_request_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: status_history; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.status_history (
    status_history_id integer NOT NULL,
    request_id integer NOT NULL,
    changed_by integer NOT NULL,
    previous_status character varying(50) NOT NULL,
    new_status character varying(50) NOT NULL,
    changed_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


--
-- Name: status_history_status_history_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.status_history ALTER COLUMN status_history_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.status_history_status_history_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: users; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.users (
    user_id integer NOT NULL,
    name character varying(100) NOT NULL,
    email character varying(255) NOT NULL,
    role character varying(50) NOT NULL
);


--
-- Name: users_user_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.users ALTER COLUMN user_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.users_user_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: assignments assignments_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.assignments
    ADD CONSTRAINT assignments_pkey PRIMARY KEY (assignment_id);


--
-- Name: categories categories_name_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT categories_name_key UNIQUE (name);


--
-- Name: categories categories_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT categories_pkey PRIMARY KEY (category_id);


--
-- Name: request_actions request_actions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.request_actions
    ADD CONSTRAINT request_actions_pkey PRIMARY KEY (action_id);


--
-- Name: resolutions resolutions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.resolutions
    ADD CONSTRAINT resolutions_pkey PRIMARY KEY (resolution_id);


--
-- Name: resolutions resolutions_request_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.resolutions
    ADD CONSTRAINT resolutions_request_id_key UNIQUE (request_id);


--
-- Name: service_requests service_requests_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_requests
    ADD CONSTRAINT service_requests_pkey PRIMARY KEY (request_id);


--
-- Name: status_history status_history_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.status_history
    ADD CONSTRAINT status_history_pkey PRIMARY KEY (status_history_id);


--
-- Name: users users_email_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_email_key UNIQUE (email);


--
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (user_id);


--
-- Name: assignments fk_assignment_request; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.assignments
    ADD CONSTRAINT fk_assignment_request FOREIGN KEY (request_id) REFERENCES public.service_requests(request_id);


--
-- Name: assignments fk_assignment_staff; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.assignments
    ADD CONSTRAINT fk_assignment_staff FOREIGN KEY (staff_id) REFERENCES public.users(user_id);


--
-- Name: request_actions fk_request_action_request; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.request_actions
    ADD CONSTRAINT fk_request_action_request FOREIGN KEY (request_id) REFERENCES public.service_requests(request_id);


--
-- Name: request_actions fk_request_action_user; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.request_actions
    ADD CONSTRAINT fk_request_action_user FOREIGN KEY (user_id) REFERENCES public.users(user_id);


--
-- Name: resolutions fk_resolution_request; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.resolutions
    ADD CONSTRAINT fk_resolution_request FOREIGN KEY (request_id) REFERENCES public.service_requests(request_id);


--
-- Name: resolutions fk_resolution_user; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.resolutions
    ADD CONSTRAINT fk_resolution_user FOREIGN KEY (resolved_by) REFERENCES public.users(user_id);


--
-- Name: service_requests fk_service_request_category; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_requests
    ADD CONSTRAINT fk_service_request_category FOREIGN KEY (category_id) REFERENCES public.categories(category_id);


--
-- Name: service_requests fk_service_request_requester; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_requests
    ADD CONSTRAINT fk_service_request_requester FOREIGN KEY (requester_id) REFERENCES public.users(user_id);


--
-- Name: status_history fk_status_history_request; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.status_history
    ADD CONSTRAINT fk_status_history_request FOREIGN KEY (request_id) REFERENCES public.service_requests(request_id);


--
-- Name: status_history fk_status_history_user; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.status_history
    ADD CONSTRAINT fk_status_history_user FOREIGN KEY (changed_by) REFERENCES public.users(user_id);


--
-- PostgreSQL database dump complete
--

\unrestrict fukbVPBAxhIsBYRrVnQx4bLL8xtkZpSVC5EBrTZPlyY4uX9mKEKn8PPGTKjuiKc
