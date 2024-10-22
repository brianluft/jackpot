# <img src="../src/J.App/Resources/App.png" width=24 height=24> Jackpot

## Personal video library stored in the cloud

Jackpot is a free, open-source Windows app for storing and streaming your video collection directly from [Backblaze B2 cloud storage](https://www.backblaze.com/cloud-storage). No server or cloud components are needed. Jackpot runs on your local computer and talks directly to B2.

You need an account with Backblaze B2 and you'll pay them for storage. Any S3-compatible storage will work, but B2 is the cheapest.

Provider | Price | Monthly egress | Egress overage fee
-- | -- | -- | --
🥇 Backblaze B2 | $6/TB | 3x total storage | $0.01/GB
🥈 Cloudflare R2 | $15/TB | Unlimited | &mdash;
💸 Amazon S3 | Up to $23/TB | 100GB | $0.09/GB

Read more about egress fees in the Backblaze article, ["Cloud 101: Data Egress Fees Explained."](https://www.backblaze.com/blog/cloud-101-data-egress-fees-explained/)

## Features

- Flexible tagging.
- Browse movies through a wall of video thumbnails.
- Stream movies using the VLC player. Stream multiple videos at once, if you want.
- Fullscreen, mouse-driven user interface featuring action buttons along the edges of the screen, following [Fitts's law](https://en.wikipedia.org/wiki/Fitts%27s_law).
- Export your library as a folder of `.m3u8` playlist files for use with VLC on tvOS/iOS and other computers on your local network that don't have Jackpot. They will stream through Jackpot running on your main computer.
- End-to-end encryption using standard AES-encrypted `.zip` files.

## Getting started

Install the following software first, if you don't already have it.

- [**VLC**](https://www.videolan.org/vlc/)
- [**ffmpeg**](https://ffmpeg.org/)
    - For x64 computers:
        1. Download `ffmpeg-release-essentials.zip` from [gyan.dev](https://www.gyan.dev/ffmpeg/builds/). 
        1. Extract somewhere on your PC, for example, `C:\Program Files\ffmpeg`.
        1. Add the `bin` sub-folder to your `PATH` environment variable by pressing Start and searching for "Edit the system environment variables".
    - For Arm64 computers:
        1. Download `ffmpeg-wos-arm64.zip` from [dvhh/ffmpeg-wos-arm64-build](https://github.com/dvhh/ffmpeg-wos-arm64-build/releases).
        1. Extract somewhere on your PC, for example, `C:\Program Files\ffmpeg`.
        1. Add the extracted folder to your `PATH` environment variable by pressing Start and searching for "Edit the system environment variables".

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
