# Pairing and Credentials

Companion Link pairing uses HAP pair-setup and establishes long-term credentials. Store the
resulting credentials securely and persist the Companion Link stable identifier alongside them.

## Stable client identifier

The `_i` field sent in `_systemInfo` must be stable across connections. Regenerating it can stop
power-state events and, on newer tvOS releases, can cause a connection to be dropped shortly after
a successful handshake.

Generate the identifier once during pairing, store it with the credentials, and reuse it for every
future connection. The WPF reference host's `CredentialStore` demonstrates this persistence model.

## Export and import

Hosts should provide their own secure credential export/import mechanism when users need to move a
paired configuration between installations. Do not commit paired-device credential files or private
keys to source control.

## Security

Pairing credentials grant control access to the paired Apple TV. Treat them like any other
application secret: restrict filesystem access, avoid logs, and revoke/re-pair if credentials are
exposed.
