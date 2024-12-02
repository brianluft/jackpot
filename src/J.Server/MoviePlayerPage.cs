using System.Net;

namespace J.Server;

public static class MoviePlayerPage
{
    public static Html GenerateHtml(string title, string m3u8Url)
    {
        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <title>{{WebUtility.HtmlEncode(title)}}</title>
                <link href="/static/video-js.min.css" rel="stylesheet">
                <script src="/static/video.min.js"></script>
                <style>
                    body { 
                        margin: 0;
                        padding: 0;
                        height: 100vh;
                        width: 100vw;
                        display: flex;
                        overflow: hidden;
                        background: #000;
                    }

                    .video-container, .video-js {
                        width: 100vw;
                        height: 100vh;
                    }
                </style>
            </head>
            <body>
                <div class="video-container">
                    <video-js id="player" class="video-js vjs-default-skin vjs-big-play-centered" tabindex="0">
                        <source src="{{WebUtility.HtmlEncode(m3u8Url)}}" type="application/x-mpegURL">
                    </video-js>
                </div>

                <script>
                    const player = videojs('player', {
                        controls: true,
                        autoplay: true,
                        muted: true,
                        controlBar: {
                            children: [
                                'playToggle',
                                'volumePanel',
                                'currentTimeDisplay',
                                'timeDivider',
                                'durationDisplay',
                                'progressControl',
                                'remainingTimeDisplay',
                                'fullscreenToggle'
                            ]
                        },
                        html5: {
                            hls: {
                                overrideNative: true
                            }
                        }
                    });

                    // Focus the player once it's ready
                    player.ready(function() {
                        this.el().focus();
                    });

                    // Optional: Add keyboard shortcuts for additional controls
                    player.on('keydown', function(e) {
                        switch(e.key) {
                            case ' ':  // Space bar
                                if (player.paused()) {
                                    player.play();
                                } else {
                                    player.pause();
                                }
                                break;
                            case 'ArrowLeft':  // Left arrow
                                player.currentTime(player.currentTime() - 5);
                                break;
                            case 'ArrowRight':  // Right arrow
                                player.currentTime(player.currentTime() + 5);
                                break;
                            case 'ArrowUp':  // Up arrow
                                player.volume(Math.min(player.volume() + 0.1, 1));
                                break;
                            case 'ArrowDown':  // Down arrow
                                player.volume(Math.max(player.volume() - 0.1, 0));
                                break;
                            case 'f':  // Fullscreen
                                if (player.isFullscreen()) {
                                    player.exitFullscreen();
                                } else {
                                    player.requestFullscreen();
                                }
                                break;
                            case 'l':  // Loop toggle
                                player.loop(!player.loop());
                                break;
                        }
                    });
                </script>
            </body>
            </html>
            """;

        return new(html);
    }
}
