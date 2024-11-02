# <img src="../src/J.App/Resources/App.png" width=24 height=24> Jackpot &mdash; personal cloud media library

Jackpot is a Windows app for storing your media collection in S3-compatible cloud storage and streaming it to your computer.

<img src="img/main-screenshot.jpg" width=640 height=360>

## Features

- Direct streaming from cloud storage. No intermediate server needed.
- Flexible tagging and filtering.
- Browse through a wall of moving video thumbnails.
- Stream movies using the VLC player. Stream multiple videos at once, if you want.
- Fullscreen, mouse-driven user interface featuring action buttons along the edges of the screen, following [Fitts's law](https://en.wikipedia.org/wiki/Fitts%27s_law).
- Export your library as a folder of `.m3u8` playlist files for use with VLC on tvOS/iOS and other computers on your local network that don't have Jackpot. They will stream through Jackpot running on your main computer.
- End-to-end encryption using standard AES-encrypted `.zip` files.

## Pricing

The Jackpot app is free, but you need an account with an S3-compatible cloud storage provider.
You'll pay them each month per terabyte of storage.
Some providers also charge per gigabyte downloaded ("egress") beyond a certain allotment per month.
Uploads to the cloud are always free.

Provider | Storage price | Included downloads | Extra downloads
-- | -- | -- | --
<nobr>🥇 [Backblaze B2](https://www.backblaze.com/cloud-storage)</nobr> | $6/TB/month | 3x total storage/month | $0.01/GB
<nobr>🥈 [Cloudflare R2](https://www.cloudflare.com/developer-platform/r2/)</nobr> | $15/TB/month | Unlimited | &mdash;
<nobr>💸 [Amazon S3](https://aws.amazon.com/s3/)</nobr> | Up to $23/TB/month | 100 GB/month | $0.09/GB

Read more about egress fees in the Backblaze article, ["Cloud 101: Data Egress Fees Explained."](https://www.backblaze.com/blog/cloud-101-data-egress-fees-explained/)

## Getting started

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
