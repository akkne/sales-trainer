#!/usr/bin/env python3
"""Shared C# source scanning used by the repository's lint scripts.

A line-oriented linter that greps raw text cannot tell code from prose. Both
codestyle-lint.py and tenancy-pool-lint.py need the same primitive: walk a line while
tracking string, char and verbatim-string literals, so a // inside a URL is not mistaken
for a comment and a rule name quoted inside a /// summary is not mistaken for the thing
the rule forbids.

CODESTYLE.md section 9 requires /// documentation on service and infrastructure classes,
so an invariant such as "never register this with AddDbContextPool" is expected to appear
as prose in exactly the files a ban on that call has to police. Comment-blind scanning
turns every such summary into a false positive.
"""

SKIP_NAME_MARKERS = (".g.cs", ".designer.cs", "globalusings", "assemblyinfo")

SKIP_DIRECTORY_NAMES = {"obj", "bin", "migrations"}


def is_skipped(path) -> bool:
    """True for generated files and for obj/, bin/ and Migrations/ trees."""
    lowered = path.name.lower()
    if any(marker in lowered for marker in SKIP_NAME_MARKERS):
        return True
    parts = {part.lower() for part in path.parts}
    return bool(parts & SKIP_DIRECTORY_NAMES)


def strip_strings_and_find_comment(line: str):
    """Returns (comment_column, code_without_strings_or_comments).

    Walks the line tracking string/char literals so a // inside a string or a URL is not
    mistaken for a comment. comment_column is the index of the first real comment marker,
    or None. A /// documentation marker is not reported as a comment: rule 9 allows it.
    """
    index = 0
    length = len(line)
    code_characters = []
    in_string = False
    in_char = False
    in_verbatim = False
    while index < length:
        character = line[index]
        following = line[index + 1] if index + 1 < length else ""
        if in_string:
            if in_verbatim:
                if character == '"' and following == '"':
                    index += 2
                    continue
                if character == '"':
                    in_string = False
                    in_verbatim = False
            else:
                if character == "\\":
                    index += 2
                    continue
                if character == '"':
                    in_string = False
            index += 1
            continue
        if in_char:
            if character == "\\":
                index += 2
                continue
            if character == "'":
                in_char = False
            index += 1
            continue
        if character == '"':
            in_string = True
            in_verbatim = line[index - 1] == "@" if index > 0 else False
            index += 1
            continue
        if character == "'":
            in_char = True
            index += 1
            continue
        if character == "/" and following == "/":
            if line[index:index + 3] == "///" and line[index:index + 4] != "////":
                return None, "".join(code_characters)
            return index, "".join(code_characters)
        if character == "/" and following == "*":
            return index, "".join(code_characters)
        code_characters.append(character)
        index += 1
    return None, "".join(code_characters)


def iterate_code_lines(text: str):
    """Yields (line_number, code) for each line, with comments and literals removed.

    Tracks /* */ blocks across lines so prose inside one is never returned as code.
    """
    in_block_comment = False
    for line_number, line in enumerate(text.splitlines(), start=1):
        if in_block_comment:
            if "*/" in line:
                in_block_comment = False
                remainder = line.split("*/", 1)[1]
                _, code = strip_strings_and_find_comment(remainder)
                yield line_number, code
            continue
        comment_column, code = strip_strings_and_find_comment(line)
        if comment_column is not None and line[comment_column:comment_column + 2] == "/*" \
                and "*/" not in line[comment_column:]:
            in_block_comment = True
        yield line_number, code
