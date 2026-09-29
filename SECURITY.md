# Security Policy

## Supported versions

Only the [latest release](https://github.com/RohanGupta15/grappler/releases/latest) gets security fixes. Installed copies update themselves, so please update before reporting.

## Reporting a vulnerability

Please **don't** open a public issue. Report it privately instead:

1. Go to the repository's **Security** tab.
2. Click **Report a vulnerability** and describe the problem, how to reproduce it, and its impact.

You should hear back within a week. Once a fix is released you'll be credited in the release notes, unless you'd rather not be.

## What's in scope

- How the app downloads, verifies (SHA-256) and runs yt-dlp, FFmpeg and Deno
- How the app passes links and options to yt-dlp (for example argument injection from a crafted link)
- The installer, the MSIX package and the self-update process
- Files the app writes or deletes in your Downloads and data folders

Bugs in yt-dlp, FFmpeg or Deno themselves belong with those projects.
