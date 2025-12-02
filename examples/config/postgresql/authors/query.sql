-- name: GetAuthor :one
SELECT * FROM authors
WHERE name = $1 LIMIT 1;

-- name: GetAuthorEmbed :one
SELECT sqlc.embed(authors) FROM authors
WHERE name = $1 LIMIT 1;

-- name: ListAuthors :many
SELECT *
FROM authors
ORDER BY name
LIMIT
    sqlc.arg('limit')
    OFFSET sqlc.arg('offset');

-- name: UpdateAuthorStatus :exec
UPDATE authors
SET status = $1
WHERE id = $2;

-- name: CreateAuthor :one
INSERT INTO authors (id, name, bio) VALUES ($1, $2, $3) RETURNING *;

-- name: CreateAuthorIncludingComment :one
INSERT INTO authors (
    id, -- this is an id
    name, -- this is a name!@#$%,
    bio -- comment?
    ) VALUES ($1, $2, $3) RETURNING *;

-- name: CreateAuthorReturnId :execlastid
INSERT INTO authors (name, bio) VALUES ($1, $2) RETURNING id;

-- name: CreateAuthorEmbed :one
INSERT INTO authors (id, name, bio) VALUES ($1, $2, $3) RETURNING sqlc.embed(authors);

-- name: GetAuthorById :one
SELECT * FROM authors
WHERE id = $1 LIMIT 1;

-- name: GetAuthorByNamePattern :many
SELECT * FROM authors
WHERE name LIKE COALESCE(sqlc.narg('name_pattern'), '%');

-- name: DeleteAuthor :exec
DELETE FROM authors
WHERE name = $1;

-- name: TruncateAuthors :exec
TRUNCATE TABLE authors CASCADE;

-- name: UpdateAuthors :execrows
UPDATE authors
SET bio = $1
WHERE bio IS NOT NULL;

-- name: GetAuthorsByIds :many
SELECT * FROM authors
WHERE id = ANY($1::BIGINT []);

-- name: GetAuthorsByIdsAndNames :many
SELECT *
FROM authors
WHERE id = ANY($1::BIGINT []) AND name = ANY($2::TEXT []);;

-- name: CreateBook :execlastid
INSERT INTO books (name, author_id) VALUES ($1, $2) RETURNING id;

-- name: ListAllAuthorsBooks :many 
SELECT
    sqlc.embed(authors),
    sqlc.embed(books)
FROM authors
INNER JOIN books ON authors.id = books.author_id
ORDER BY authors.name;

-- name: GetDuplicateAuthors :many 
SELECT
    sqlc.embed(authors1),
    sqlc.embed(authors2)
FROM authors AS authors1
INNER JOIN authors AS authors2 ON authors1.name = authors2.name
WHERE authors1.id < authors2.id;

-- name: GetAuthorsByBookName :many 
SELECT
    authors.*,
    sqlc.embed(books)
FROM authors INNER JOIN books ON authors.id = books.author_id
WHERE books.name = $1;

-- name: CreateExtendedBio :exec
INSERT INTO extended.bios (author_name, name, bio_type) VALUES ($1, $2, $3);

-- name: GetFirstExtendedBioByType :one
SELECT * FROM extended.bios WHERE bio_type = $1 LIMIT 1;

-- name: TruncateExtendedBios :exec
TRUNCATE TABLE extended.bios;

-- name: GetAuthorsWithDuplicateParams :many
-- This query demonstrates parameter deduplication where the same parameter is used multiple times
SELECT * FROM authors 
WHERE (name = sqlc.narg('author_name') OR bio LIKE '%' || sqlc.narg('author_name') || '%')
  AND (id > sqlc.narg('min_id') OR id < sqlc.narg('min_id') + 1000)
  AND created_at >= sqlc.narg('date_filter')
  AND updated_at >= sqlc.narg('date_filter');

-- name: GetAuthorWithPentaParam :one
-- This query uses the same parameter five times to test extensive deduplication
SELECT * FROM authors 
WHERE name = sqlc.narg('search_value') 
   OR bio LIKE '%' || sqlc.narg('search_value') || '%'
   OR CAST(id AS TEXT) = sqlc.narg('search_value')
   OR created_at::TEXT LIKE '%' || sqlc.narg('search_value') || '%'
   OR (LENGTH(sqlc.narg('search_value')) > 0 AND name IS NOT NULL)
LIMIT 1;

-- name: CreateAuthorWithMetadata :one
INSERT INTO authors (id, name, bio, metadata) VALUES ($1, $2, $3, $4) RETURNING *;

-- name: GetAuthorsWithJsonMetadata :many
SELECT
    sqlc.embed(authors),
    books.name as book_name
FROM authors
LEFT JOIN books ON authors.id = books.author_id
WHERE authors.metadata IS NOT NULL
ORDER BY authors.name;
