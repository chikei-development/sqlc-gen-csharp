CREATE TYPE authors_status AS ENUM ('active', 'inactive', 'pending');

CREATE TABLE authors (
    id BIGSERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    bio TEXT,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    metadata JSON,
    status authors_status DEFAULT 'pending'
);

CREATE TABLE books (
    id UUID PRIMARY KEY DEFAULT UUID_GENERATE_V4(),
    name TEXT NOT NULL,
    author_id BIGINT NOT NULL,
    description TEXT,
    FOREIGN KEY (author_id) REFERENCES authors (id) ON DELETE CASCADE
);

CREATE TABLE "user" (
    "id" SERIAL PRIMARY KEY,
    "updated_at" TIMESTAMP WITH TIME ZONE
);

CREATE SCHEMA extended;

CREATE TYPE extended.bio_type AS ENUM ('Autobiography', 'Biography', 'Memoir');

CREATE TABLE extended.bios (
    author_name VARCHAR(100),
    name VARCHAR(100),
    bio_type extended.bio_type,
    PRIMARY KEY (author_name, name)
);
