# Security

DungeonTable is a free hobby project, so answers are best effort, and fixes go into the latest release
only.

## Reporting a vulnerability

Please report it privately, with **Report a vulnerability** on the repository's
[Security tab](https://github.com/Gemberkoekje/DungeonTable/security), not in a public issue. Say what
someone can do with it, and show how: the request, the setting or the file that does it.

## What counts

The app is meant to be served at a URL, behind one shared passphrase (see the
[README](README.md#security)). A vulnerability is anything that gets past the passphrase, or that lets
someone who has it do more than run the table:

- read or write files outside the content folder;
- run code on the server;
- make it serve something other than the app and the content's maps and pictures (the `/art` route
  serves raster images only, and an upload is decoded and re-encoded before it is stored).

These are not vulnerabilities:

- **What the player view's page holds.** The player view is a projector, not something players browse
  on their own devices, and the passphrase is shared with the table. Hidden rooms and secret doors may be
  in the page it is sent; what matters is that the projector does not show them. When it does, that is a
  bug: report it as an ordinary issue.
- **A weak passphrase, or the app served over plain HTTP.** The passphrase travels with every request,
  which is why the README says to put the app behind HTTPS.
