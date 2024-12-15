CREATE TABLE movies (
	id TEXT PRIMARY KEY, -- e.g. 'movie-...'
	filename TEXT NOT NULL, -- original filename as provided by the user
	s3_key TEXT NOT NULL,
	date_added TEXT NOT NULL
) WITHOUT ROWID;

CREATE TABLE tag_types (
	id TEXT PRIMARY KEY, -- e.g. 'tagtype-...'
	sort_index INTEGER NOT NULL,
	singular_name TEXT NOT NULL,
	plural_name TEXT NOT NULL
) WITHOUT ROWID;

CREATE TABLE tags (
	id TEXT PRIMARY KEY, -- e.g. 'tag-...'
	tag_type_id TEXT NOT NULL,
	name TEXT NOT NULL,
	FOREIGN KEY (tag_type_id) REFERENCES tag_types (id)
) WITHOUT ROWID;

CREATE TABLE movie_tags (
	movie_id TEXT NOT NULL,
	tag_id TEXT NOT NULL,
	PRIMARY KEY (movie_id, tag_id),
	FOREIGN KEY (movie_id) REFERENCES movies (id),
	FOREIGN KEY (tag_id) REFERENCES tags (id)
) WITHOUT ROWID;

CREATE INDEX idx_tag_movies
	ON movie_tags (tag_id, movie_id);

CREATE TABLE movie_files (
	movie_id TEXT NOT NULL,
	name TEXT NOT NULL, -- blank string means the zip header
	offset INTEGER NOT NULL,
	length INTEGER NOT NULL,
	data BLOB NULL, -- provided only for entries '', 'movie.m3u8', 'clip.mp4'
	PRIMARY KEY (movie_id, name),
	FOREIGN KEY (movie_id) REFERENCES movies (id)
) WITHOUT ROWID;

CREATE TABLE remote_version (
	version_id TEXT PRIMARY KEY
) WITHOUT ROWID;
