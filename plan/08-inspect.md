# 08 — Inspect

**Status:** built.

**Goal:** read a certificate, a JWT, or a checksum without leaving the commander, and duplicate
the selection in place. Each one is a page or a copy. None of them grows a second application.

The usage map is a mode of the same panel. It is described at the end of this note.

## View certificate

F3 on a certificate file opens a summary in the same full-screen viewer as text. The palette
command **View certificate** opens the cursor file the same way when the name is unusual. Esc
or F3 closes it.

Types:

- One certificate, PEM or DER (`.pem`, `.crt`, `.cer`, `.der`)
- A PEM bundle, leaf first when the file is one chain
- PKCS#7 (`.p7b`, `.p7c`)
- PKCS#12 (`.pfx`, `.p12`)
- A certificate request (`.csr`), with a first line that says it is a request
- A public key, as algorithm and size

The page shows subject, issuer, serial, the validity window, a status line (`Expired`,
`Not valid yet`, or `Valid for N days`), signature algorithm, public key type and size,
SHA-256 fingerprint, DNS names, whether it is a CA, and key usage. A bundle is numbered.
A PKCS#12 says whether a private key is inside and never prints the key.

A password-protected PKCS#12 gets one masked prompt. An empty password is tried first, so a
bag with no password does not ask. The key is loaded ephemerally where the platform allows
it. macOS rejects that flag, so the certificate is loaded with the default and disposed when
the page is built, which drops the key. The page never prints the key.

A private key (`.key`, or a PEM that is only a key) is one line: `Private key. Not shown.`
OpenSSH certificates are a different format and stay out.

F3 on a file that only looks like a certificate by its extension, and is not one, falls
through to the text viewer. The palette command says `This is not a certificate.` and stops.

Files over 1 MB are refused, the same cap as the text viewer.

## View JWT

**View JWT** is a palette command. It opens a field. The clipboard is the starting text when
the clipboard already holds a token. Otherwise a cursor file that is a single token is the
starting text. Enter shows the decoded header and payload. Esc cancels.

`exp`, `nbf`, and `iat` are shown in local time with a short phrase (`expired 3 days ago`,
`expires in 5 hours`). The signature is reported as present and is not checked. `alg` of
`none` is called out on its own line. Five segments is an encrypted token: the page says so
and stops. The token is not written to a log and is not sent anywhere.

F3 uses the same decoder when the file is a `.jwt` or the whole text is one token.

A paste longer than 64 KB is refused.

## Checksum

**Checksum** is a palette command. It hashes the effective selection with SHA-256 and opens
the viewer. One file is the name and the hash. A marked set is one pair of lines each.
Directories are `Not a file.` The hash is the whole file, streamed, with no size cap.

## Duplicate

**Duplicate** is on the File menu and in the palette. It copies the effective selection into
the folder it already occupies. The first free name is `notes copy.md`, then
`notes copy 2.md`, and it does not ask about overwrite. Folders copy recursively through the
existing copy job. It has no function key.

F5 into the same folder still asks `Duplicate as notes copy.md?` and still uses that exact
name. The palette command is the one that walks to a free name.

## Usage map

The command palette opens a usage map over the panels. It is not on a menu and it has no
key. A directory's area is its recursive size. A file's area is its own size. Enter drills
in, Left returns to the parent with the cursor on the folder you left, and tiny items
become one `other` block. Another volume is one block and is not entered. The scan runs in
the background. Esc closes the map and cancels a scan still running. Symlinks are not
followed. Unreadable folders are skipped and the footer counts them. After a scan of the
folder the panel is showing, the list shows those folder sizes in place of `<DIR>`. Opening
a panel does not scan.

## Keys

View certificate, View JWT, and Checksum are on the File menu and in the palette. Duplicate
is there too. None of them take a function key. The usage map is palette-only. F3 grows the
certificate and JWT cases. The palette and F1 list the commands, because both are generated
from the command table.
