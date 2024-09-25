# Jackpot

## Personal cloud video library

Jackpot is a client-only Windows application for browsing your cloud library with full motion video thumbnails.
No server or cloud components are needed beyond the S3-compatible bucket.

Jackpot is free, open source, and does not require an account.
You need an account with a cloud storage provider and you'll pay them for storage:

- 🥇 Backblaze B2: $6/TB with free egress up to 3x of your total storage size
- 🥈 Cloudflare R2: $15/TB with unlimited free egress
- 💸 Amazon S3: up to $23/TB with $0.09/GB egress—a poor choice for this!

Read more about egress fees in the Backblaze article, ["Cloud 101: Data Egress Fees Explained."](https://www.backblaze.com/blog/cloud-101-data-egress-fees-explained/)

## Getting started

Install the following software first, if you don't already have it.

- VLC
    - Jackpot uses VLC for video playback.
    - Install from the [VLC website](https://www.videolan.org/vlc/) or the Microsoft Store.
- ffmpeg
    - Jackpot uses ffmpeg to generate video thumbnails.
    - x64: Download `ffmpeg-release-essentials.zip` from [gyan.dev](https://www.gyan.dev/ffmpeg/builds/). Extract somewhere on your PC. Add the `bin` folder to your `PATH` environment variable by pressing Start and searching for "Edit the system environment variables".
    - Arm64: Download `ffmpeg-wos-arm64.zip` from [dvhh/ffmpeg-wos-arm64-build](https://github.com/dvhh/ffmpeg-wos-arm64-build/releases). Extract somewhere on your PC. Add the extracted folder to your `PATH` environment variable by pressing Start and searching for "Edit the system environment variables".

On the Backblaze website, create a bucket and an application key.

Download Jackpot, extract, and run `J.App.exe`.
If the Account Settings window does not appear, click the menu button in the upper left corner to access it.
Enter, at minimum:
- Endpoint URL
- Bucket name
- Access key ID and secret access key
- Choose a password for encrypting your library

Save your settings, then click "Connect" under the menu.
You're in!
Next time, Jackpot will connect automatically.

Begin importing your movies using "Add Movies..." in the menu.
