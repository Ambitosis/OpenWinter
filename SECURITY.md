# Security policy

## Supported versions

OpenWinter is pre-1.0. Security fixes go into the `dev` branch and the next release; older releases are not
patched. Run the latest release, especially for a public server.

## Reporting a vulnerability

Report vulnerabilities privately through GitHub: open the repository's **Security** tab and choose
**Report a vulnerability**. Please don't open a public issue, discussion or pull request for a security problem.

Include the OpenWinter version, what an attacker can do, and the steps or a proof of concept to reproduce it. You
should get a reply within a week. Once a fix is released, the advisory is published with credit to you unless you'd
rather stay anonymous.

## Scope

Especially of interest, since persistent-world servers are exposed to the internet:

- The dedicated server's network protocol: crashes, hangs or memory exhaustion caused by a client, bypassing the
  version handshake, or acting for another player.
- Content download: a server writing outside the client's content cache, or a client fetching files a server did not
  offer.
- Script functions that read or write files or databases (campaign database, SQLite, NWNX emulation) escaping their
  sandboxed folders.
- The server vault: reading, changing or deleting another player's characters.

Out of scope: cheating that the original game also allows, problems in modules or haks themselves, and denial of
service by flooding a server's network link.
