# Conventions

## No comments in code (2026-07-29)

Reinforcing CLAUDE.md rule 2: `.cs` files carry no comments of any kind —
no `//`, `/* */`, or `///` XML doc comments. Anything worth explaining
(design rationale, non-obvious behaviour, tuning notes) goes in a
`/docs/*.md` file instead, keyed to the class/system it describes.
