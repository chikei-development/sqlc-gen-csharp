CREATE TABLE authors (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    name TEXT NOT NULL,
    bio TEXT,
    status ENUM('active', 'inactive', 'pending') DEFAULT 'pending'
);

CREATE TABLE books (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    name TEXT NOT NULL,
    author_id BIGINT NOT NULL,
    description TEXT,
    FOREIGN KEY (author_id) REFERENCES authors (id) ON DELETE CASCADE
);

CREATE TABLE `user` (
    `id` INT PRIMARY KEY AUTO_INCREMENT,
    `updated_at` TIMESTAMP
);

CREATE SCHEMA extended;

CREATE TABLE extended.bios (
    author_name VARCHAR(100),
    name VARCHAR(100),
    bio_type ENUM('Autobiography', 'Biography', 'Memoir'),
    author_type SET('Author', 'Editor', 'Translator'),
    PRIMARY KEY (author_name, name)
);
