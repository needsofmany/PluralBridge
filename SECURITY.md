# Security Policy

PluralBridge handles private Simply Plural data. Treat all exported files, API tokens, avatar images, notes, and generated databases as sensitive personal data.

## API Tokens

Your Simply Plural / Apparyllis API token is private.

Do not:

- commit it to Git
- paste it into public issues or discussions
- include it in screenshots
- share it with anyone you do not completely trust
- store it in example files with real values

PluralBridge documentation and scripts are designed to use environment variables or local configuration files for tokens.

Recommended token environment variable:

```text
SP_TOKEN
```
