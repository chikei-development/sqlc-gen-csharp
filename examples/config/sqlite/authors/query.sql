-- name: GetAuthor :one
SELECT * FROM authors WHERE name = ? LIMIT 1;

-- name: ListAuthors :many
SELECT * 
FROM authors
ORDER BY name
LIMIT ? OFFSET ?;

-- name: CreateAuthor :exec
INSERT INTO authors (id, name, bio) VALUES (?, ?, ?);

-- name: CreateAuthorIncludingComment :exec
INSERT INTO authors (
    id, -- this is an id
    name, -- this is a name!@#$%,
    bio -- comment?
) VALUES (?, ?, ?);

-- name: CreateAuthorReturnId :execlastid
INSERT INTO authors (name, bio) VALUES (?, ?) RETURNING id;

-- name: GetAuthorById :one
SELECT * FROM authors WHERE id = ? LIMIT 1;

-- name: GetAuthorByNamePattern :many
SELECT * FROM authors
WHERE name LIKE COALESCE(sqlc.narg('name_pattern'), '%');

-- name: DeleteAuthor :exec
DELETE FROM authors
WHERE name = ?;

-- name: DeleteAllAuthors :exec
DELETE FROM authors;

-- name: UpdateAuthors :execrows
UPDATE authors
SET bio = sqlc.arg('bio')
WHERE bio IS NOT NULL;

-- name: GetAuthorsByIds :many
SELECT * FROM authors WHERE id IN (sqlc.slice('ids'));

-- name: GetAuthorsByIdsAndNames :many
SELECT * FROM authors WHERE id IN (sqlc.slice('ids')) AND name IN (sqlc.slice('names'));

-- name: CreateBook :execlastid
INSERT INTO books (name, author_id) VALUES (?, ?) RETURNING id;

-- name: ListAllAuthorsBooks :many 
SELECT sqlc.embed(authors), sqlc.embed(books) 
FROM authors JOIN books ON authors.id = books.author_id 
ORDER BY authors.name;

-- name: GetDuplicateAuthors :many 
SELECT sqlc.embed(authors1), sqlc.embed(authors2)
FROM authors authors1 JOIN authors authors2 ON authors1.name = authors2.name
WHERE authors1.id < authors2.id;

-- name: GetAuthorsByBookName :many 
SELECT authors.*, sqlc.embed(books)
FROM authors JOIN books ON authors.id = books.author_id
WHERE books.name = ?;

-- name: GetAuthorByIdWithMultipleNamedParam :one
SELECT * FROM authors WHERE id = sqlc.arg('id_arg') AND id = sqlc.arg('id_arg') LIMIT sqlc.narg('take');

-- name: GetAuthorsWithDuplicateParams :many
-- This query demonstrates parameter deduplication where the same parameter is used multiple times
SELECT * FROM authors 
WHERE (name = sqlc.narg('author_name') OR bio LIKE '%' || sqlc.narg('author_name') || '%')
  AND (id > sqlc.narg('min_id') OR id < sqlc.narg('min_id') + 1000);

-- name: GetAuthorWithTripleNameParam :one
-- This query uses the same parameter three times to test extensive deduplication
SELECT * FROM authors 
WHERE name = sqlc.narg('author_name') 
   OR bio LIKE '%' || sqlc.narg('author_name') || '%'
   OR CAST(id AS TEXT) LIKE '%' || sqlc.narg('author_name') || '%'
LIMIT 1;

-- name: GetAuthorWithQuadrupleParam :one
-- This query uses the same parameter four times to test extensive deduplication
SELECT * FROM authors 
WHERE name = sqlc.narg('search_term') 
   OR bio LIKE '%' || sqlc.narg('search_term') || '%'
   OR CAST(id AS TEXT) = sqlc.narg('search_term')
   OR (LENGTH(sqlc.narg('search_term')) > 0 AND name IS NOT NULL)
LIMIT 1;