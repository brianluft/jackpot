# Jackpot

## Cloud-native personal video library

Jackpot is a free, open-source Windows app for storing and streaming your video collection directly from Backblaze B2. It is truly serverless: no server or cloud components are needed. Jackpot runs on your local computer and talks directly to B2.

You need an account with Backblaze B2 (or another S3-compatible provider) and you'll pay them for storage based on their rates:

🥇 Backblaze B2: $6/TB with free monthly egress up to 3x of your total storage size, then $0.01/GB.<br>
🥈 Cloudflare R2: $15/TB with unlimited free egress.<br>
💸 Amazon S3: up to $23/TB with 100GB free monthly egress, then $0.09/GB.<br>

Read more about egress fees in the Backblaze article, ["Cloud 101: Data Egress Fees Explained."](https://www.backblaze.com/blog/cloud-101-data-egress-fees-explained/)

## Features

- Flexible tagging.
- Browse movies through a wall of video thumbnails.
- Stream movies using the VLC player. Stream multiple videos at once, if you want.
- Fullscreen, mouse-driven user interface featuring action buttons along the edges of the screen, following [Fitts's law](https://en.wikipedia.org/wiki/Fitts%27s_law).
- Export your library as a folder of `.m3u8` playlist files for use with VLC on tvOS/iOS and other computers on your local network that don't have Jackpot. They will stream through Jackpot running on your main computer.

## Getting started

Install the following software first, if you don't already have it.

- [VLC](https://www.videolan.org/vlc/)
- [ffmpeg](https://ffmpeg.org/)
    - **x64**: Download `ffmpeg-release-essentials.zip` from [gyan.dev](https://www.gyan.dev/ffmpeg/builds/). Extract somewhere on your PC, for example, `C:\Program Files\ffmpeg`. Add the `bin` sub-folder to your `PATH` environment variable by pressing Start and searching for "Edit the system environment variables".
    - **Arm64**: Download `ffmpeg-wos-arm64.zip` from [dvhh/ffmpeg-wos-arm64-build](https://github.com/dvhh/ffmpeg-wos-arm64-build/releases). Extract somewhere on your PC, for example, `C:\Program Files\ffmpeg`. Add the extracted folder to your `PATH` environment variable by pressing Start and searching for "Edit the system environment variables".

On the Backblaze website, create a bucket and an application key.

Download Jackpot, extract, and run `Jackpot.exe`.
If the Account Settings window does not appear, click the menu button in the upper left corner to access it.
Enter, at minimum:
- Endpoint URL
- Bucket name
- Access key ID and secret access key
- Choose a password for encrypting your library

Save your settings, then click "Connect" under the menu.
You're in!
Next time, Jackpot will connect automatically.
