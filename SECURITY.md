# Security Policy

## Supported Versions

TeleFlow has not reached its first stable public release yet.

Before `3.0.0`, security fixes are made on the active development branch and included in the next prerelease or release candidate.

After the first stable release, the latest stable minor line is the supported line unless a release note says otherwise.

## Reporting a Vulnerability

Use GitHub private vulnerability reporting for this repository.

Do not open a public issue with exploit details, bot tokens, private chat data, production logs, or credentials.

Include:
- affected package and version;
- minimal reproduction or affected API surface;
- expected impact;
- whether a workaround exists;
- any relevant environment details such as OS, architecture, .NET runtime, transport, and hosting mode.

## Scope

Security-sensitive areas include:
- Telegram token handling;
- webhook request validation;
- generated Telegram client serialization and multipart upload behavior;
- package publishing and supply chain integrity;
- state storage isolation;
- handler routing behavior that could bypass documented filters or authorization;
- denial-of-service risks in routing, parsing, callbacks, state, or transport loops.

## Disclosure

Security fixes should be released with clear notes once a fix is available. Public details should not be disclosed before maintainers have had a reasonable chance to patch and publish.
